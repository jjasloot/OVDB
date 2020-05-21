using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OV_DB.Models;
using OVDB_database.Database;
using OVDB_database.Models;
using SharpKml.Base;
using SharpKml.Dom;
using SharpKml.Engine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RoutesController : ControllerBase
    {
        private readonly OVDBDatabaseContext _context;

        public RoutesController(OVDBDatabaseContext context)
        {
            _context = context;
        }

        // GET: api/RoutesAPI
        [HttpGet]
        [EnableQuery]

        public async Task<ActionResult<IEnumerable<Route>>> GetRoutes([FromQuery] int? start, [FromQuery] int? count, [FromQuery] string sortColumn, [FromQuery] bool? descending, [FromQuery] string filter)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var query = _context.Routes
                .Include(r => r.RouteType)
                .Include(r => r.RouteMaps)
                .ThenInclude(r => r.Map)
                .Where(r => r.RouteMaps.Any(rm => rm.Map.UserId == userIdClaim));

            if (!string.IsNullOrWhiteSpace(sortColumn))
            {
                if (sortColumn == "name")
                {
                    if (descending.GetValueOrDefault(false))
                        query = query.OrderByDescending(r => r.Name);
                    else
                        query = query.OrderBy(r => r.Name);
                }
                if (sortColumn == "date")
                {
                    if (descending.GetValueOrDefault(false))
                        query = query.OrderByDescending(r => r.FirstDateTime);
                    else
                        query = query.OrderBy(r => r.FirstDateTime);
                }
                if (sortColumn == "type")
                {
                    if (descending.GetValueOrDefault(false))
                        query = query.OrderByDescending(r => r.RouteType.Name);
                    else
                        query = query.OrderBy(r => r.RouteType.Name);
                }
                if (sortColumn == "maps")
                {
                    if (descending.GetValueOrDefault(false))
                        query = query.OrderByDescending(r => r.RouteMaps.FirstOrDefault().Map.Name ?? "");
                    else
                        query = query.OrderBy(r => r.RouteMaps.FirstOrDefault().Map.Name ?? "");
                }
            }

            if (!string.IsNullOrWhiteSpace(filter))
            {
                query = query.Where(r => EF.Functions.Like(r.Name, "%" + filter + "%") || EF.Functions.Like(r.RouteType.Name, "%" + filter + "%") || r.RouteMaps.Any(rm => EF.Functions.Like(rm.Map.Name, "%" + filter + "%")));
            }

            if (start.HasValue && count.HasValue)
            {
                query = query
                    .Skip(start.Value)
                    .Take(count.Value);
            }
            return await query.ToListAsync();

        }
        [HttpGet("count")]
        public async Task<ActionResult<int>> GetRouteCount([FromQuery] string filter)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var query = _context.Routes
                .Where(r => r.RouteMaps.Any(rm => rm.Map.UserId == userIdClaim));
            if (!string.IsNullOrWhiteSpace(filter))
            {
                query = query.Where(r => EF.Functions.Like(r.Name, "%" + filter + "%") || EF.Functions.Like(r.RouteType.Name, "%" + filter + "%"));
            }
            return await query.CountAsync();
        }

        [HttpGet("missingInfo")]
        public async Task<ActionResult<IEnumerable<Route>>> GetRoutesWithMissingInfo()
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            return await _context.Routes
                .Where(r => r.RouteMaps.Any(rm => rm.Map.UserId == userIdClaim))
                .Where(r => r.RouteTypeId == null)
                .ToListAsync();
        }

        [HttpGet("years")]
        public async Task<ActionResult<IEnumerable<int?>>> GetRoutesYears()
        {
            var years = await _context.Routes
                .Where(r => r.RouteTypeId != null)
                .Select(r => r.FirstDateTime.HasValue ? (int?)r.FirstDateTime.Value.Year : null)
                .Distinct()
                .ToListAsync();
            return years;
        }
        // GET: api/RoutesAPI/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Route>> GetRoute(int id)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var route = await _context.Routes
                .Where(r => r.RouteMaps.Any(rm => rm.Map.UserId == userIdClaim))
                .Include(r => r.RouteCountries)
                .Include(r => r.RouteMaps)
                .SingleAsync(r => r.RouteId == id);
            if (route == null)
            {
                return NotFound();
            }
            route.Coordinates = null;
            return route;
        }

        // PUT: api/RoutesAPI/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
        [HttpPut("{id}")]
        public async Task<IActionResult> PutRoute(int id, UpdateRoute route)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            if (id != route.RouteId)
            {
                return BadRequest();
            }

            var dbRoute = await _context.Routes
                .Where(r => r.RouteMaps.Any(rm => rm.Map.UserId == userIdClaim))
                .Include(r => r.RouteCountries)
                .Include(r => r.RouteMaps)
                .SingleOrDefaultAsync(r => r.RouteId == id);
            if (dbRoute is null)
            {
                return NotFound();
            }
            dbRoute.Description = route.Description;
            dbRoute.DescriptionNL = route.DescriptionNL;
            dbRoute.FirstDateTime = route.FirstDateTime;
            dbRoute.LineNumber = route.LineNumber;
            dbRoute.Name = route.Name;
            dbRoute.NameNL = route.NameNL;
            dbRoute.OverrideColour = route.OverrideColour;
            dbRoute.OperatingCompany = route.OperatingCompany;
            dbRoute.RouteTypeId = route.RouteTypeId;

            if (route.Countries != null)
            {
                var toDelete = new List<RouteCountry>();
                var toAdd = new List<int>();
                dbRoute.RouteCountries.ForEach(r =>
                {
                    if (!route.Countries.Contains(r.CountryId))
                    {
                        toDelete.Add(r);
                    }
                });
                if (route.Countries != null)
                {
                    route.Countries.ForEach(r =>
                    {
                        if (!dbRoute.RouteCountries.Select(r => r.CountryId).Contains(r))
                        {
                            toAdd.Add(r);
                        }
                    });
                }
                if (toDelete.Any())
                {
                    _context.RoutesCountries.RemoveRange(toDelete);
                }

                if (toAdd.Any())
                {
                    _context.RoutesCountries.AddRange(toAdd.Select(c => new RouteCountry { CountryId = c, RouteId = id }));
                }
            }

            if (route.Maps != null)
            {
                var toDelete = new List<RouteMap>();
                var toAdd = new List<int>();
                dbRoute.RouteMaps.ForEach(r =>
                {
                    if (!route.Maps.Contains(r.MapId))
                    {
                        toDelete.Add(r);
                    }
                });
                if (route.Maps != null)
                {
                    route.Maps.ForEach(r =>
                    {
                        if (!dbRoute.RouteMaps.Select(r => r.MapId).Contains(r))
                        {
                            toAdd.Add(r);
                        }
                    });
                }
                if (toDelete.Any())
                {
                    _context.RoutesMaps.RemoveRange(toDelete);
                }

                if (toAdd.Any())
                {
                    _context.RoutesMaps.AddRange(toAdd.Select(m => new RouteMap { MapId = m, RouteId = id }));
                }
            }
            _context.Routes.Update(dbRoute);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!RouteExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }


        [HttpPost("kml")]
        public async Task<ActionResult<Route>> PostKML()
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }

            using var stream = new StreamReader(HttpContext.Request.Body);

            var route = (await PostKmlToDatabase(stream, "Nieuwe route", userIdClaim)).Single();

            return CreatedAtAction("GetRoute", new { id = route.RouteId }, route);
        }

        [HttpPost("kmlfile")]
        public async Task<ActionResult<Route>> PostKMLFiles()
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var files = Request.Form.Files;

            var size = Request.Form.Files.Sum(f => f.Length);
            var filePaths = new List<string>();
            var filesUploaded = new List<FileUpload>();

            foreach (var formFile in files)
            {
                if (formFile.Length > 0)
                {
                    using var stream = new StreamReader(formFile.OpenReadStream());
                    try
                    {
                        List<Route> routes = new List<Route>();
                        if (formFile.FileName.EndsWith(".kml"))
                        {
                            routes.AddRange(await PostKmlToDatabase(stream, formFile.FileName, userIdClaim));
                        }
                        if (formFile.FileName.EndsWith(".gpx"))
                        {
                            routes.AddRange(await PostGpxToDatabase(stream, userIdClaim, formFile.FileName, userIdClaim));
                        }
                        routes.ForEach(route =>
                        {
                            filesUploaded.Add(new FileUpload
                            {
                                Filename = formFile.FileName,
                                RouteId = route.RouteId
                            });
                        });
                    }
                    catch (Exception ex)
                    {
                        filesUploaded.Add(new FileUpload
                        {
                            Filename = formFile.FileName,
                            Failed = true
                        });
                    }
                }
            }

            return Ok(filesUploaded);
        }

        private async Task<List<Route>> PostKmlToDatabase(StreamReader stream, string fileName, int userId)
        {
            var body = stream.ReadToEnd();

            var parser = new Parser();
            parser.ParseString(body, false);
            var kml = parser.Root as Kml;
            var placeMarks = kml.Flatten().Where(e => e.GetType() == typeof(Placemark)).ToList();
            var routes = new List<Route>();
            foreach (Placemark placeMark in placeMarks)
            {
                var child = (LineString)placeMark.Flatten().Single(e => e.GetType() == typeof(LineString));
                var coordinates = child.Coordinates.Select(c => "" + c.Longitude.ToString(CultureInfo.InvariantCulture) + "," + c.Latitude.ToString(CultureInfo.InvariantCulture)).ToList();

                var name = placeMark.Name;
                if (string.Equals(name, "Tessellated", StringComparison.OrdinalIgnoreCase))
                {
                    name = fileName;
                    if (name.EndsWith(".kml"))
                    {
                        name = name.Substring(0, name.Length - 4);
                    }
                }
                var route = new Route
                {
                    Coordinates = string.Join('\n', coordinates),
                    Name = name,
                    RouteMaps = new List<RouteMap>()
                };
                if (placeMark.Description != null)
                {
                    route.Description = placeMark.Description.Text;
                }

                var types = await _context.RouteTypes.Where(r => r.UserId == userId).ToListAsync();

                if (placeMark.ExtendedData != null)
                {
                    var extendedData = placeMark.ExtendedData.Data;
                    if (extendedData.Any(d => d.Name == "firstDateTime"))
                    {
                        route.FirstDateTime = DateTime.ParseExact(extendedData.Single(d => d.Name == "firstDateTime").Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    }

                    if (extendedData.Any(d => d.Name == "type"))
                    {
                        var typeString = extendedData.Single(d => d.Name == "type").Value;
                        var type = types.SingleOrDefault(t => string.Compare(t.Name, typeString.Trim(), true) == 0 || string.Compare(t.NameNL, typeString.Trim(), true) == 0);
                        if (type != null)
                        {
                            route.RouteTypeId = type.TypeId;
                        }
                        else
                        {
                            var newRouteType = new RouteType
                            {
                                Colour = "#ff0000",
                                UserId = userId,
                                Name = typeString.Trim()
                            };
                            route.RouteType = newRouteType;
                        }
                    }
                    if (extendedData.Any(d => d.Name == "line"))
                    {
                        route.LineNumber = extendedData.Single(d => d.Name == "line").Value;
                    }
                    if (extendedData.Any(d => d.Name == "nameNL"))
                    {
                        route.NameNL = extendedData.Single(d => d.Name == "nameNL").Value;
                    }
                    if (extendedData.Any(d => d.Name == "descriptionNL"))
                    {
                        route.DescriptionNL = extendedData.Single(d => d.Name == "descriptionNL").Value;
                    }
                    if (extendedData.Any(d => d.Name == "color"))
                    {
                        route.OverrideColour = extendedData.Single(d => d.Name == "color").Value;
                    }
                    if (extendedData.Any(d => d.Name == "company"))
                    {
                        route.OperatingCompany = extendedData.Single(d => d.Name == "company").Value;
                    }

                    if (extendedData.Any(d => d.Name == "countries"))
                    {
                        route.RouteCountries = new List<RouteCountry>();
                        var dbCountries = await _context.Countries.Where(r => r.UserId == userId).ToListAsync();
                        var countries = extendedData.Single(d => d.Name == "countries").Value.Split(',').ToList();
                        countries.ForEach(inputCountry =>
                        {
                            var county = dbCountries.SingleOrDefault(c => string.Compare(c.Name, inputCountry.Trim(), true) == 0 || string.Compare(c.NameNL, inputCountry.Trim(), true) == 0);
                            if (county != null)
                            {
                                route.RouteCountries.Add(new RouteCountry { CountryId = county.CountryId });
                            }
                            else
                            {
                                var newCountry = new Country
                                {
                                    Name = inputCountry.Trim(),
                                    UserId = userId
                                };
                                route.RouteCountries.Add(new RouteCountry { Country = newCountry });
                            }
                        });
                    }

                    if (extendedData.Any(d => d.Name == "maps"))
                    {
                        route.RouteMaps = new List<RouteMap>();
                        var dbMaps = await _context.Maps.Where(r => r.UserId == userId).ToListAsync();
                        var inputMaps = extendedData.Single(d => d.Name == "maps").Value.Split(',').ToList();
                        inputMaps.ForEach(inputMap =>
                        {
                            var map = dbMaps.SingleOrDefault(c => string.Compare(c.Name, inputMap.Trim(), true) == 0);
                            if (map != null)
                            {
                                route.RouteMaps.Add(new RouteMap { MapId = map.MapId });
                            }
                            else
                            {
                                var newMap = new Map
                                {
                                    Name = inputMap.Trim(),
                                    MapGuid = Guid.NewGuid(),
                                    UserId = userId,
                                    Default = false
                                };
                                route.RouteMaps.Add(new RouteMap { Map = newMap });
                            }
                        });
                    }
                }
                if (!route.RouteMaps.Any())
                {
                    var maps = await _context.Maps.Where(m => m.UserId == userId).ToListAsync();

                    var defaultMap = maps.Where(m => m.Default == true).FirstOrDefault();
                    if (defaultMap == null)
                    {
                        defaultMap = maps.First();
                    }

                    route.RouteMaps.Add(new RouteMap { MapId = defaultMap.MapId });
                }

                routes.Add(route);
                _context.Routes.Add(route);
                await _context.SaveChangesAsync();
            }
            return routes;
        }



        private async Task<List<Route>> PostGpxToDatabase(StreamReader stream, int mapId, string fileName, int userId)
        {

            var gpxReader = new Gpx.GpxReader(stream.BaseStream);
            var read = gpxReader.Read();
            if (!read)
            {
                throw new Exception("Cannot read file");
            }
            var name = gpxReader.Track.Name;
            if (string.Equals(name, "Tessellated", StringComparison.OrdinalIgnoreCase))
            {
                name = fileName;
                if (name.EndsWith(".gpx"))
                {
                    name = name.Substring(0, name.Length - 4);
                }
            }
            var metadata = gpxReader.Metadata;
            var coordinates = gpxReader.Track.ToGpxPoints().Select(p => p.Longitude.ToString(CultureInfo.InvariantCulture) + "," + p.Latitude.ToString(CultureInfo.InvariantCulture)).ToList();
            var route = new Route
            {
                Coordinates = string.Join('\n', coordinates),
                Name = name,
                RouteMaps = new List<RouteMap>
                {
                    new RouteMap
                    {
                        MapId=mapId
                    }
                }
            };
            if (!string.IsNullOrWhiteSpace(gpxReader.Track.Description))
            {
                route.Description = gpxReader.Track.Description;
            }


            var maps = await _context.Maps.Where(m => m.UserId == userId).ToListAsync();

            var defaultMap = maps.Where(m => m.Default == true).SingleOrDefault();
            if (defaultMap == null)
            {
                defaultMap = maps.First();
            }


            _context.Add(route);

            await _context.SaveChangesAsync();
            return new List<Route> { route };
        }



        // DELETE: api/RoutesAPI/5
        [HttpDelete("{id}")]
        public async Task<ActionResult<Route>> DeleteRoute(int id)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var route = await _context.Routes
                .Where(r => r.RouteMaps.Any(rm => rm.Map.UserId == userIdClaim))
                .SingleOrDefaultAsync(r => r.RouteId == id);
            if (route == null)
            {
                return NotFound();
            }
            var routeMaps = await _context.RoutesMaps.Where(rm => rm.RouteId == id).ToListAsync();
            _context.RoutesMaps.RemoveRange(routeMaps);

            var routeCountries = await _context.RoutesCountries.Where(rc => rc.RouteId == id).ToListAsync();
            _context.RoutesCountries.RemoveRange(routeCountries);

            _context.Routes.Remove(route);
            await _context.SaveChangesAsync();

            return route;
        }

        [HttpGet("{id}/export")]
        public async Task<ActionResult<string>> ExportRoute(int id)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var route = await _context.Routes
                .Include(r => r.RouteType)
                .Include(r => r.RouteCountries)
                .ThenInclude(rc => rc.Country)
                .Include(r => r.RouteMaps)
                .ThenInclude(rm => rm.Map)
                .Where(r => r.RouteMaps.Any(rm => rm.Map.UserId == userIdClaim))
                .SingleOrDefaultAsync(r => r.RouteId == id);
            if (route == null)
            {
                return NotFound();
            }

     


            var folder = new Folder
            {
                Name = "Export van OVDB"
            };
            AddRouteToKmlFolder(folder, route);
            var document = new Document();
            document.AddFeature(folder);
            var kml = new Kml
            {
                Feature = document
            };

            Serializer serializer = new Serializer(SerializerOptions.ReadableFloatingPoints);
            serializer.Serialize(kml);
            return File(Encoding.UTF8.GetBytes(serializer.Xml), "application/xml");
        }
        [HttpGet("export")]
        public async Task<ActionResult<string>> ExportAllRoutes(int id)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var routes = await _context.Routes
                .Include(r => r.RouteType)
                .Include(r => r.RouteCountries)
                .ThenInclude(rc => rc.Country)
                .Include(r => r.RouteMaps)
                .ThenInclude(rm => rm.Map)
                .Where(r => r.RouteMaps.Any(rm => rm.Map.UserId == userIdClaim))
                .ToListAsync();
            if (routes == null)
            {
                return NotFound();
            }
            var folder = new Folder
            {
                Name = "Export van OVDB",
                Description = new Description { Text = DateTime.UtcNow.ToString("o") }
            };
            foreach (var route in routes)
            {
                AddRouteToKmlFolder(folder, route);
            }


            var document = new Document();
            document.AddFeature(folder);
            var kml = new Kml
            {
                Feature = document
            };

            Serializer serializer = new Serializer(SerializerOptions.ReadableFloatingPoints);
            serializer.Serialize(kml);

            return File(Encoding.UTF8.GetBytes(serializer.Xml), "application/xml");
        }

        private static void AddRouteToKmlFolder(Folder folder, Route route)
        {
            var lineString = new LineString
            {
                Tessellate = true,
                Coordinates = new CoordinateCollection(route.Coordinates
                .Split("\n")
                .Select(str => new Vector
                {
                    Longitude = Math.Round(double.Parse(str.Split(",").First(), CultureInfo.InvariantCulture), 8),
                    Latitude = Math.Round(double.Parse(str.Split(",").Last(), CultureInfo.InvariantCulture), 8)
                }))
            };
            var placemark = new Placemark
            {
                Geometry = lineString,
                Name = route.Name
            };
            if (!string.IsNullOrWhiteSpace(route.Description))
            {
                placemark.Description = new Description
                {
                    Text = route.Description
                };
            }
            placemark.ExtendedData = new ExtendedData();
            if (route.FirstDateTime.HasValue)
            {
                placemark.ExtendedData.AddData(new Data { Name = "firstDateTime", Value = route.FirstDateTime.Value.ToString("yyyy-MM-dd") });
            }
            if (route.RouteType != null)
            {
                placemark.ExtendedData.AddData(new Data { Name = "type", Value = route.RouteType.Name });
            }
            if (route.RouteCountries.Any())
            {
                placemark.ExtendedData.AddData(new Data { Name = "countries", Value = string.Join(',', route.RouteCountries.Select(rc => rc.Country.Name)) });
            }
            if (!string.IsNullOrWhiteSpace(route.LineNumber))
            {
                placemark.ExtendedData.AddData(new Data { Name = "line", Value = route.LineNumber });
            }
            if (!string.IsNullOrWhiteSpace(route.NameNL))
            {
                placemark.ExtendedData.AddData(new Data { Name = "nameNL", Value = route.NameNL });
            }
            if (!string.IsNullOrWhiteSpace(route.DescriptionNL))
            {
                placemark.ExtendedData.AddData(new Data { Name = "descriptionNL", Value = route.DescriptionNL });
            }
            if (!string.IsNullOrWhiteSpace(route.OperatingCompany))
            {
                placemark.ExtendedData.AddData(new Data { Name = "company", Value = route.OperatingCompany });
            }
            if (!string.IsNullOrWhiteSpace(route.OverrideColour))
            {
                placemark.ExtendedData.AddData(new Data { Name = "color", Value = route.OverrideColour });
            }
            if (route.RouteMaps.Any())
            {
                placemark.ExtendedData.AddData(new Data { Name = "maps", Value = string.Join(',', route.RouteMaps.Select(rm => rm.Map.Name)) });
            }
            folder.AddFeature(placemark);
        }

        private bool RouteExists(int id)
        {
            return _context.Routes.Any(e => e.RouteId == id);
        }
    }
}
