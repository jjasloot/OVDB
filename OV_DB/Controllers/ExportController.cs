using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OV_DB.Helpers;
using OV_DB.Models;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using NetTopologySuite.Geometries;

namespace OV_DB.Controllers
{
    [Microsoft.AspNetCore.Mvc.Route("api/[controller]")]
    [ApiController]
    public class ExportController : ControllerBase
    {
        private readonly OVDBDatabaseContext _dbContext;

        public ExportController(OVDBDatabaseContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpPost("Trainlog")]
        public async Task<IActionResult> ExportToTrainlog([FromBody] ExportRequest request)
        {
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            if ((request.RouteInstanceIds == null || !request.RouteInstanceIds.Any()) &&
                (request.RouteIds == null || !request.RouteIds.Any()))
            {
                return BadRequest("No routes selected");
            }

            IQueryable<RouteInstance> query = _dbContext.RouteInstances
                .Include(ri => ri.Route)
                    .ThenInclude(r => r.RouteType)
                .Include(ri => ri.Route)
                    .ThenInclude(r => r.Regions)
                        .ThenInclude(rg => rg.ParentRegion)
                            .ThenInclude(pr => pr.ParentRegion)
                .Include(ri => ri.RouteInstanceProperties);

            if (request.RouteIds != null && request.RouteIds.Any())
            {
                query = query.Where(ri => request.RouteIds.Contains(ri.RouteId));
            }
            else if (request.RouteInstanceIds != null && request.RouteInstanceIds.Any())
            {
                query = query.Where(ri => request.RouteInstanceIds.Contains(ri.RouteInstanceId));
            }

            var instances = await query.ToListAsync();

            var records = new List<TrainlogExportRow>();
            var stationFlags = new Dictionary<string, string>(); // Cache for station flags

            foreach (var instance in instances)
            {
                var route = instance.Route;
                var properties = instance.RouteInstanceProperties.ToDictionary(p => p.Key, p => p.Value);

                // Collect all regions including parents (needed for Countries and Flags)
                var allRegions = new HashSet<Region>();
                if (route.Regions != null)
                {
                    foreach (var r in route.Regions.Where(r => !string.IsNullOrWhiteSpace(r.IsoCode)))
                    {
                        allRegions.Add(r);
                    }
                }

                // 1. Determine Type
                string type = "bus"; // Default
                if (!string.IsNullOrEmpty(route.TrainlogType))
                {
                    type = route.TrainlogType;
                }
                else if (route.RouteType != null)
                {
                    if (!string.IsNullOrEmpty(route.RouteType.TrainlogType))
                    {
                        type = route.RouteType.TrainlogType;
                    }
                    else if (route.RouteType.IsTrain)
                    {
                        type = "train";
                    }
                }

                // 2. Dates
                DateTime start;
                DateTime end;

                if (instance.StartTime.HasValue)
                {
                    // StartTime and EndTime are typically just times on 0001-01-01, but sometimes full dates?
                    // Check if StartTime year is 1. If so, combine with instance.Date.
                    if (instance.StartTime.Value.Year == 1)
                    {
                        start = instance.Date.Date.Add(instance.StartTime.Value.TimeOfDay);
                    }
                    else
                    {
                        start = instance.StartTime.Value;
                    }
                }
                else
                {
                    start = instance.Date.Date;
                }

                if (instance.EndTime.HasValue)
                {
                    if (instance.EndTime.Value.Year == 1)
                    {
                        end = instance.Date.Date.Add(instance.EndTime.Value.TimeOfDay);
                    }
                    else
                    {
                        end = instance.EndTime.Value;
                    }

                    // Handle overnight trips
                    if (end < start)
                    {
                        end = end.AddDays(1);
                    }
                }
                else
                {
                    end = start.AddHours(1); // Default duration?
                }

                // 3. Flags/Stations
                string fromFlag = "";
                string toFlag = "";

                if (route.LineString != null && route.LineString.Coordinates.Length > 0)
                {
                    var startPoint = new Point(route.LineString.Coordinates.First());
                    var endPoint = new Point(route.LineString.Coordinates.Last());

                    fromFlag = GetFlagFromRegions(startPoint, allRegions);
                    toFlag = GetFlagFromRegions(endPoint, allRegions);
                }

                string origin = AppendFlag(fromFlag, route.From);
                string destination = AppendFlag(toFlag, route.To);

                // 4. Other fields
                var duration = (end - start).TotalMinutes;
                var lengthKm = route.OverrideDistance ?? route.CalculatedDistance;
                var lengthMeters = (lengthKm) * 1000;
                var encodedPath = PolylineHelper.Encode(route.LineString);
                if (string.IsNullOrEmpty(encodedPath))
                {
                    // Trainlog import requires a path. Default to (0,0) encoded as "??".
                    encodedPath = "??";
                }

                // Waypoints: Convert route coordinates to JSON list of {lat, lng}
                // Reduce number of waypoints to prevent 500 error on import (likely payload size or processing limit)
                string waypointsJson = "[]";
                if (route.LineString != null && route.LineString.Coordinates.Any())
                {
                    // Use Douglas-Peucker simplification from NetTopologySuite
                    // DistanceTolerance is in degrees. 0.001 is approx 100m. 0.0001 is approx 10m.
                    // Start with 0.0001 to keep reasonable detail but reduce point count.
                    var simplified = NetTopologySuite.Simplify.DouglasPeuckerSimplifier.Simplify(route.LineString, 0.001);

                    var waypointsList = simplified.Coordinates
                        .Select(c => new { lat = c.Y, lng = c.X })
                        .ToList();
                    waypointsJson = System.Text.Json.JsonSerializer.Serialize(waypointsList);
                    // Trainlog specific escaping:
                    // 1. Escape quotes with backslash
                    // 2. Wrap entire JSON in quotes
                    // This results in """...""" in the CSV file
                    waypointsJson = $"\"{waypointsJson.Replace("\"", "\\\"")}\"";
                }

                // Countries: Infer from Route Regions
                // Trainlog format: {"CODE": distance_in_meters, ...}
                string countriesJson = "{}";

                // Only process regions that have an ISO code
                var isoRegions = allRegions.Where(r => !string.IsNullOrEmpty(r.IsoCode)).ToList();
                if (isoRegions != null && isoRegions.Any() && route.LineString != null)
                {
                    var countries = new Dictionary<string, double>();

                    // We need to intersect the route LineString with each Region's Geometry to get precise distance.
                    // Note: This operation can be computationally expensive.

                    foreach (var region in isoRegions)
                    {
                        try
                        {
                            if (region.Geometry == null) continue;

                            var intersection = region.Geometry.Intersection(route.LineString);
                            if (intersection != null && !intersection.IsEmpty)
                            {
                                // Length is in degrees (approximately), we need meters.
                                // Quick approximation: length * 111319.9 (meters per degree at equator)
                                // But better to rely on percentage of total length?

                                // Let's use ratio of intersection length to total line length
                                double totalLengthGeo = route.LineString.Length;
                                double intersectionLengthGeo = intersection.Length;

                                if (totalLengthGeo > 0)
                                {
                                    double ratio = intersectionLengthGeo / totalLengthGeo;
                                    double metersInRegion = lengthMeters * ratio;

                                    if (metersInRegion > 1) // Ignore tiny slivers
                                    {
                                        if (countries.ContainsKey(region.IsoCode))
                                        {
                                            countries[region.IsoCode] += metersInRegion;
                                        }
                                        else
                                        {
                                            countries[region.IsoCode] = metersInRegion;
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Fallback if geometry operation fails
                        }
                    }

                    // Fallback: If no intersection found (or no geometries loaded), use the old logic
                    if (!countries.Any())
                    {
                        var countryCodes = new HashSet<string>();
                        var fromStation = await _dbContext.Stations.Include(s => s.StationCountry).FirstOrDefaultAsync(s => s.Name == route.From);
                        var toStation = await _dbContext.Stations.Include(s => s.StationCountry).FirstOrDefaultAsync(s => s.Name == route.To);

                        // Use IsoCode from Region if possible, else StationCountry
                        if (fromStation?.StationCountry != null) countryCodes.Add(fromStation.StationCountry.NameNL ?? fromStation.StationCountry.Name);
                        if (toStation?.StationCountry != null) countryCodes.Add(toStation.StationCountry.NameNL ?? toStation.StationCountry.Name);

                        if (countryCodes.Any())
                        {
                            double distPerCountry = lengthMeters / countryCodes.Count;
                            foreach (var code in countryCodes)
                            {
                                countries[code] = distPerCountry;
                            }
                        }
                    }

                    if (countries.Any())
                    {
                        countriesJson = System.Text.Json.JsonSerializer.Serialize(countries);
                    }
                }
                else if (isoRegions != null && isoRegions.Any())
                {
                    // Fallback if no LineString but we have ISO regions
                    var countries = new Dictionary<string, double>();
                    var countryCodes = new HashSet<string>();

                    // Collect codes from regions without geometries
                    foreach (var r in isoRegions)
                    {
                        countryCodes.Add(r.IsoCode);
                    }

                    if (countryCodes.Any())
                    {
                        double distPerCountry = lengthMeters / countryCodes.Count;
                        foreach (var code in countryCodes)
                        {
                            countries[code] = distPerCountry;
                        }
                        countriesJson = System.Text.Json.JsonSerializer.Serialize(countries);
                    }
                }

                // Tags
                string materialType = properties.ContainsKey("Voertuig type") ? properties["Voertuig type"] :
                                     (properties.ContainsKey("train_type") ? properties["train_type"] : "");
                string reg = properties.ContainsKey("Voertuig nummer") ? properties["Voertuig nummer"] : "";
                string note = "";

                // Create Row Object
                records.Add(new TrainlogExportRow
                {
                    Uid = instance.RouteInstanceId.ToString(),
                    Username = "ovdb_export",
                    OriginStation = origin,
                    DestinationStation = destination,
                    StartDatetime = start.ToString("yyyy-MM-dd HH:mm:ss"),
                    EndDatetime = end.ToString("yyyy-MM-dd HH:mm:ss"),
                    EstimatedTripDuration = duration.ToString("F0"),
                    ManualTripDuration = "",
                    TripLength = lengthMeters.ToString("F0"),
                    Operator = route.OperatingCompany ?? "",
                    Countries = countriesJson,
                    UtcStartDatetime = start.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    UtcEndDatetime = end.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    LineName = route.LineNumber ?? "",
                    Created = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    LastModified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Type = type,
                    MaterialType = materialType,
                    Seat = "",
                    Reg = reg,
                    Waypoints = waypointsJson,
                    Notes = note,
                    Price = "",
                    Currency = "",
                    PurchasingDate = "",
                    Visibility = "public",
                    Path = encodedPath
                });
            }

            using (var writer = new StringWriter())
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(records);
                var bytes = Encoding.UTF8.GetBytes(writer.ToString());
                return File(bytes, "text/csv", "trainlog_export.csv");
            }
        }

        private string GetFlagFromRegions(Point point, IEnumerable<Region> regions)
        {
            // Find region containing point with a flag
            var match = regions.FirstOrDefault(r => !string.IsNullOrEmpty(r.FlagEmoji) && r.Geometry != null && r.Geometry.Contains(point));
            return match?.FlagEmoji ?? "";
        }

        private string AppendFlag(string flag, string name)
        {
            if (!string.IsNullOrEmpty(flag))
            {
                return $"{flag} {name}";
            }
            return name;
        }

        public class TrainlogExportRow
        {
            [Name("uid")]
            public string Uid { get; set; }
            [Name("username")]
            public string Username { get; set; }
            [Name("origin_station")]
            public string OriginStation { get; set; }
            [Name("destination_station")]
            public string DestinationStation { get; set; }
            [Name("start_datetime")]
            public string StartDatetime { get; set; }
            [Name("end_datetime")]
            public string EndDatetime { get; set; }
            [Name("estimated_trip_duration")]
            public string EstimatedTripDuration { get; set; }
            [Name("manual_trip_duration")]
            public string ManualTripDuration { get; set; }
            [Name("trip_length")]
            public string TripLength { get; set; }
            [Name("operator")]
            public string Operator { get; set; }
            [Name("countries")]
            public string Countries { get; set; }
            [Name("utc_start_datetime")]
            public string UtcStartDatetime { get; set; }
            [Name("utc_end_datetime")]
            public string UtcEndDatetime { get; set; }
            [Name("line_name")]
            public string LineName { get; set; }
            [Name("created")]
            public string Created { get; set; }
            [Name("last_modified")]
            public string LastModified { get; set; }
            [Name("type")]
            public string Type { get; set; }
            [Name("material_type")]
            public string MaterialType { get; set; }
            [Name("seat")]
            public string Seat { get; set; }
            [Name("reg")]
            public string Reg { get; set; }
            [Name("waypoints")]
            public string Waypoints { get; set; }
            [Name("notes")]
            public string Notes { get; set; }
            [Name("price")]
            public string Price { get; set; }
            [Name("currency")]
            public string Currency { get; set; }
            [Name("purchasing_date")]
            public string PurchasingDate { get; set; }
            [Name("visibility")]
            public string Visibility { get; set; }
            [Name("path")]
            public string Path { get; set; }
        }

        public class ExportRequest
        {
            public List<int> RouteInstanceIds { get; set; }
            public List<int> RouteIds { get; set; }
        }
    }
}
