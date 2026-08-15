using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using OV_DB.Helpers;
using OV_DB.Models;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OV_DB.Services;

public interface ITrainlogExportService
{
    Task<byte[]> BuildTrainlogCsvAsync(int userId, ExportRequest request);
}

public class TrainlogExportService(OVDBDatabaseContext dbContext, ILogger<TrainlogExportService> logger) : ITrainlogExportService
{
    public async Task<byte[]> BuildTrainlogCsvAsync(int userId, ExportRequest request)
    {
        var user = await dbContext.Users.FindAsync(userId);

        IQueryable<RouteInstance> query = dbContext.RouteInstances
            .Include(ri => ri.Route)
                .ThenInclude(r => r.RouteType)
            .Include(ri => ri.Route)
                .ThenInclude(r => r.Regions)
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

        var operatorMappingDict = ParseOperatorMappings(user?.TrainlogOperatorMappings);

        var records = new List<TrainlogExportRow>();

        foreach (var instance in instances)
        {
            records.Add(await BuildRowAsync(instance, user, operatorMappingDict));
        }

        using var writer = new StringWriter();
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteRecords(records);
        return Encoding.UTF8.GetBytes(writer.ToString());
    }

    private Dictionary<string, string> ParseOperatorMappings(string json)
    {
        var operatorMappingDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
            return operatorMappingDict;
        try
        {
            var mappings = System.Text.Json.JsonSerializer.Deserialize<List<TrainlogOperatorMappingDTO>>(json);
            if (mappings != null)
            {
                foreach (var m in mappings)
                {
                    if (!string.IsNullOrWhiteSpace(m.OvdbOperator))
                        operatorMappingDict[m.OvdbOperator] = m.TrainlogOperator ?? m.OvdbOperator;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not parse TrainlogOperatorMappings; exporting without operator mapping");
        }
        return operatorMappingDict;
    }

    private async Task<TrainlogExportRow> BuildRowAsync(RouteInstance instance, User user, Dictionary<string, string> operatorMappingDict)
    {
        var route = instance.Route;
        var properties = instance.RouteInstanceProperties.ToDictionary(p => p.Key, p => p.Value);

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
            if (instance.StartTime.Value.Year == 1)
                start = instance.Date.Date.Add(instance.StartTime.Value.TimeOfDay);
            else
                start = instance.StartTime.Value;
        }
        else
        {
            start = instance.Date.Date;
        }

        if (instance.EndTime.HasValue)
        {
            if (instance.EndTime.Value.Year == 1)
                end = instance.Date.Date.Add(instance.EndTime.Value.TimeOfDay);
            else
                end = instance.EndTime.Value;

            if (end < start)
                end = end.AddDays(1);
        }
        else
        {
            end = start;
        }

        // Scheduled times (used for start_datetime/end_datetime when available)
        DateTime? scheduledStart = null;
        DateTime? scheduledEnd = null;

        if (instance.ScheduledStartTime.HasValue)
        {
            scheduledStart = instance.ScheduledStartTime.Value.Year == 1
                ? instance.Date.Date.Add(instance.ScheduledStartTime.Value.TimeOfDay)
                : instance.ScheduledStartTime.Value;
        }

        if (instance.ScheduledEndTime.HasValue)
        {
            var raw = instance.ScheduledEndTime.Value.Year == 1
                ? instance.Date.Date.Add(instance.ScheduledEndTime.Value.TimeOfDay)
                : instance.ScheduledEndTime.Value;
            if (raw < (scheduledStart ?? start))
                raw = raw.AddDays(1);
            scheduledEnd = raw;
        }

        // Delay in seconds (actual - scheduled, negative = early)
        string departureDelay = scheduledStart.HasValue && instance.StartTime.HasValue
            ? ((int)Math.Round((start - scheduledStart.Value).TotalSeconds)).ToString()
            : "";
        string arrivalDelay = scheduledEnd.HasValue && instance.EndTime.HasValue
            ? ((int)Math.Round((end - scheduledEnd.Value).TotalSeconds)).ToString()
            : "";

        // If scheduled times are available, use them for start_datetime/end_datetime
        DateTime exportStart = scheduledStart ?? start;
        DateTime exportEnd = scheduledEnd ?? end;

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
        string waypointsJson = "[]";
        if (route.LineString != null && route.LineString.Coordinates.Any())
        {
            var simplified = NetTopologySuite.Simplify.DouglasPeuckerSimplifier.Simplify(route.LineString, 0.001);

            var waypointsList = simplified.Coordinates
                .Select(c => new { lat = c.Y, lng = c.X })
                .ToList();
            waypointsJson = System.Text.Json.JsonSerializer.Serialize(waypointsList);
            waypointsJson = $"\"{waypointsJson.Replace("\"", "\\\"")}\"";
        }

        string countriesJson = await ComputeCountriesJsonAsync(route, allRegions, lengthMeters);

        // Tags
        string materialKey = !string.IsNullOrEmpty(user?.TrainlogMaterialKey) ? user.TrainlogMaterialKey : "Voertuig type";
        string regKey = !string.IsNullOrEmpty(user?.TrainlogRegistrationKey) ? user.TrainlogRegistrationKey : "Voertuig nummer";
        string seatKey = !string.IsNullOrEmpty(user?.TrainlogSeatKey) ? user.TrainlogSeatKey : "Stoel";

        string materialType = properties.ContainsKey(materialKey) ? properties[materialKey] : "";
        if (string.IsNullOrEmpty(materialType) && string.IsNullOrEmpty(user?.TrainlogMaterialKey) && properties.ContainsKey("train_type"))
        {
            materialType = properties["train_type"];
        }

        string reg = properties.ContainsKey(regKey) ? properties[regKey] : "";
        string note = "";

        return new TrainlogExportRow
        {
            Uid = instance.RouteInstanceId.ToString(),
            Username = "ovdb_export",
            OriginStation = origin,
            DestinationStation = destination,
            StartDatetime = exportStart == exportEnd ? exportStart.ToString("yyyy-MM-dd") : exportStart.ToString("yyyy-MM-dd HH:mm:ss"),
            EndDatetime = exportStart == exportEnd ? exportStart.ToString("yyyy-MM-dd") : exportEnd.ToString("yyyy-MM-dd HH:mm:ss"),
            EstimatedTripDuration = duration.ToString("F0"),
            ManualTripDuration = "",
            TripLength = lengthMeters.ToString("F0"),
            Operator = MapOperators(route.OperatingCompany, operatorMappingDict),
            Countries = countriesJson,
            UtcStartDatetime = exportStart == exportEnd ? exportStart.ToString("yyyy-MM-dd") : exportStart.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"),
            UtcEndDatetime = exportStart == exportEnd ? exportStart.ToString("yyyy-MM-dd") : exportEnd.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"),
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
            Path = encodedPath,
            DepartureDelay = departureDelay,
            ArrivalDelay = arrivalDelay
        };
    }

    private async Task<string> ComputeCountriesJsonAsync(Route route, HashSet<Region> allRegions, double lengthMeters)
    {
        // Countries: Infer from Route Regions. Trainlog format: {"CODE": distance_in_meters, ...}
        var isoRegions = allRegions.Where(r => !string.IsNullOrEmpty(r.IsoCode)).ToList();
        if (isoRegions.Count == 0)
            return "{}";

        var countries = new Dictionary<string, double>();

        if (route.LineString != null)
        {
            foreach (var region in isoRegions)
            {
                try
                {
                    if (region.Geometry == null) continue;

                    var intersection = region.Geometry.Intersection(route.LineString);
                    if (intersection != null && !intersection.IsEmpty)
                    {
                        double totalLengthGeo = route.LineString.Length;
                        double intersectionLengthGeo = intersection.Length;

                        if (totalLengthGeo > 0)
                        {
                            double ratio = intersectionLengthGeo / totalLengthGeo;
                            double metersInRegion = lengthMeters * ratio;

                            if (metersInRegion > 1) // Ignore tiny slivers
                            {
                                countries.TryGetValue(region.IsoCode, out var existing);
                                countries[region.IsoCode] = existing + metersInRegion;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Geometry intersection failed for region {RegionId} on route {RouteId}; skipping this country", region.Id, route.RouteId);
                }
            }

            // Fallback: If no intersection found (or no geometries loaded), use the old logic
            if (countries.Count == 0)
            {
                var countryCodes = new HashSet<string>();
                var fromStation = await dbContext.Stations.Include(s => s.StationCountry).FirstOrDefaultAsync(s => s.Name == route.From);
                var toStation = await dbContext.Stations.Include(s => s.StationCountry).FirstOrDefaultAsync(s => s.Name == route.To);

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
        }
        else
        {
            // Fallback if no LineString but we have ISO regions
            var countryCodes = new HashSet<string>();
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
            }
        }

        return countries.Any() ? System.Text.Json.JsonSerializer.Serialize(countries) : "{}";
    }

    private static string GetFlagFromRegions(Point point, IEnumerable<Region> regions)
    {
        var match = regions.FirstOrDefault(r => !string.IsNullOrEmpty(r.FlagEmoji) && r.Geometry != null && r.Geometry.Contains(point));
        return match?.FlagEmoji ?? "🇺🇳";
    }

    /// <summary>
    /// Maps OVDB operator names to their Trainlog equivalents. Since Trainlog's July 2026
    /// operators rework, the CSV `operator` column is a comma-separated list (split on ','
    /// and trimmed on import), so multi-operator routes are mapped name-by-name. A mapping
    /// keyed on the complete string still wins, so existing whole-string mappings keep working.
    /// </summary>
    private static string MapOperators(string operatingCompany, Dictionary<string, string> mappings)
    {
        if (string.IsNullOrWhiteSpace(operatingCompany))
            return "";
        if (mappings.TryGetValue(operatingCompany.Trim(), out var wholeStringMatch))
            return wholeStringMatch;

        var parts = operatingCompany.Split(',')
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .Select(p => mappings.TryGetValue(p, out var mapped) ? mapped : p);
        return string.Join(", ", parts);
    }

    private static string AppendFlag(string flag, string name)
    {
        if (!string.IsNullOrEmpty(flag))
        {
            return $"{flag} {name}";
        }
        return name;
    }
}
