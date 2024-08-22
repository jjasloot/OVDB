using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.EntityFrameworkCore;
using OV_DB.Helpers;
using OV_DB.Models;
using OV_DB.Services;
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
using System.Threading;
using System.Threading.Tasks;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RoutesController : ControllerBase
    {
        private readonly OVDBDatabaseContext _context;
        private readonly IMapper _mapper;
        private readonly IRouteRegionsService _routeRegionsService;

        public RoutesController(OVDBDatabaseContext context, IMapper mapper, IRouteRegionsService routeRegionsService)
        {
            _context = context;
            _mapper = mapper;
            _routeRegionsService = routeRegionsService;
        }

        // GET: api/RoutesAPI
        [HttpGet]
        public async Task<ActionResult<RouteListDTO>> GetRoutes([FromQuery] int? start, [FromQuery] int? count, [FromQuery] string sortColumn, [FromQuery] bool? descending, [FromQuery] string filter, CancellationToken cancellationToken)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var originalQuery = _context.Routes
                .Where(r => r.RouteMaps.Any(rm => rm.Map.UserId == userIdClaim));

            if (!string.IsNullOrWhiteSpace(filter))
            {
                originalQuery = originalQuery.Where(r => EF.Functions.Like(r.Name, "%" + filter + "%"));
            }
            var query = originalQuery.ProjectTo<RouteDTO>(_mapper.ConfigurationProvider);
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
                        query = query.OrderByDescending(r => r.RouteMaps.FirstOrDefault().Name ?? "");
                    else
                        query = query.OrderBy(r => r.RouteMaps.FirstOrDefault().Name ?? "");
                }
            }

            var routeCount = await query.CountAsync(cancellationToken: cancellationToken);


            if (start.HasValue && count.HasValue)
            {
                query = query
                    .Skip(start.Value)
                    .Take(count.Value);
            }

            var response = new RouteListDTO
            {
                Count = routeCount,
                Routes = await query.ToListAsync(cancellationToken: cancellationToken)
            };

            return response;

        }


        [HttpGet("missingInfo")]
        public async Task<ActionResult<IEnumerable<RouteDTO>>> GetRoutesWithMissingInfo()
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            return await _context.Routes
                .Where(r => r.RouteMaps.Any(rm => rm.Map.UserId == userIdClaim))
                .Where(r => r.RouteTypeId == null || (string.IsNullOrWhiteSpace(r.From) && string.IsNullOrWhiteSpace(r.To)))
                .OrderByDescending(r => r.RouteInstances.Max(ri => ri.Date))
                .ProjectTo<RouteDTO>(_mapper.ConfigurationProvider)
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
        public async Task<ActionResult<RouteDTO>> GetRoute(int id)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var route = await _context.Routes
                .Where(r => r.RouteMaps.Any(rm => rm.Map.UserId == userIdClaim))
                .Include(r => r.RouteCountries)
                .Include(r => r.RouteInstances)
                .Include(r => r.RouteMaps)
                .ProjectTo<RouteDTO>(_mapper.ConfigurationProvider)
                .SingleAsync(r => r.RouteId == id);
            if (route == null)
            {
                return NotFound();
            }
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
                .Include(r => r.RouteInstances)
                .SingleOrDefaultAsync(r => r.RouteId == id);
            if (dbRoute is null)
            {
                return NotFound();
            }
            dbRoute.Description = route.Description;
            dbRoute.DescriptionNL = route.DescriptionNL;
            dbRoute.LineNumber = route.LineNumber;
            dbRoute.Name = route.Name;
            dbRoute.NameNL = route.NameNL;
            dbRoute.From = route.From;
            dbRoute.To = route.To;
            dbRoute.OverrideColour = route.OverrideColour;
            dbRoute.OperatingCompany = route.OperatingCompany;
            dbRoute.RouteTypeId = route.RouteTypeId;
            dbRoute.OverrideDistance = route.OverrideDistance;
            dbRoute.OperatorId = route.OperatorId;
            if (route.FirstDateTime.HasValue)
            {
                if (dbRoute.RouteInstances.Count == 0)
                {
                    dbRoute.RouteInstances.Add(new RouteInstance { Date = route.FirstDateTime.Value });
                }
                else if (dbRoute.RouteInstances.Count == 1)
                {
                    dbRoute.RouteInstances.First().Date = route.FirstDateTime.Value;
                }
            }
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
            var placeMarks = kml.Flatten().Where(e => e.GetType() == typeof(Placemark)).Select(p => (Placemark)p).ToList();
            var routes = new List<Route>();
            foreach (Placemark placeMark in placeMarks.Where(pl => pl.Geometry is LineString))
            {
                var child = (LineString)placeMark.Flatten().Single(e => e.GetType() == typeof(LineString));
                var coordinates = child.Coordinates.Select(c => "" + c.Longitude.ToString(CultureInfo.InvariantCulture) + "," + c.Latitude.ToString(CultureInfo.InvariantCulture)).ToList();

                var lineString = new NetTopologySuite.Geometries.LineString(coordinates.Select(c =>
                {
                    var split = c.Split(",");
                    return new NetTopologySuite.Geometries.Coordinate(double.Parse(split[0], CultureInfo.InvariantCulture), double.Parse(split[1], CultureInfo.InvariantCulture));
                }).ToArray());


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
                    LineString = lineString,
                    Name = name,
                    RouteMaps = new List<RouteMap>(),
                    Share = Guid.NewGuid(),
                    RouteInstances = new List<RouteInstance>()
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
                        route.RouteInstances.Add(new RouteInstance { Date = DateTime.ParseExact(extendedData.Single(d => d.Name == "firstDateTime").Value, "yyyy-MM-dd", CultureInfo.InvariantCulture) });
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
                    if (extendedData.Any(d => d.Name == "from"))
                    {
                        route.From = extendedData.Single(d => d.Name == "from").Value;
                    }
                    if (extendedData.Any(d => d.Name == "to"))
                    {
                        route.To = extendedData.Single(d => d.Name == "to").Value;
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

                DistanceCalculationHelper.ComputeDistance(route);

                await _routeRegionsService.AssignRegionsToRouteAsync(route);

                routes.Add(route);
                _context.Routes.Add(route);
                await _context.SaveChangesAsync();
            }
            return routes;
        }



        private async Task<List<Route>> PostGpxToDatabase(StreamReader stream, int mapId, string fileName, int userId)
        {

            var gpxReader = new GpxReader(stream.BaseStream);
            do
            {
                var read = gpxReader.Read();
                if (!read)
                {
                    throw new Exception("Cannot read file");
                }
            } while (gpxReader.ObjectType != GpxObjectType.Track);

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

            var lineString = new NetTopologySuite.Geometries.LineString(gpxReader.Track.ToGpxPoints().Select(x => new NetTopologySuite.Geometries.Coordinate(x.Longitude, x.Latitude)).ToArray());

            var route = new Route
            {
                Coordinates = string.Join('\n', coordinates),
                LineString = lineString,
                Name = name,
                Share = Guid.NewGuid(),
                RouteMaps = new List<RouteMap>
                {
                    new RouteMap
                    {
                        MapId=mapId
                    }
                },
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
            DistanceCalculationHelper.ComputeDistance(route);
            await _routeRegionsService.AssignRegionsToRouteAsync(route);

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
        public async Task<ActionResult<string>> ExportAllRoutes(int id, [FromQuery] Guid? map, [FromQuery] int? year)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }

            var query = _context.Routes
                            .Include(r => r.RouteType)
                            .Include(r => r.RouteCountries)
                            .ThenInclude(rc => rc.Country)
                            .Include(r => r.RouteMaps)
                            .ThenInclude(rm => rm.Map)
                            .Where(r => r.RouteMaps.Any(rm => rm.Map.UserId == userIdClaim));
            if (map.HasValue)
            {
                query = query.Where(r => r.RouteInstances.Any(ri => ri.RouteInstanceMaps.Any(rim => rim.Map.MapGuid == map)) || r.RouteMaps.Any(rm => rm.Map.MapGuid == map));
            }
            if (year.HasValue)
            {
                query = query.Where(r => r.RouteInstances.Any(ri => ri.Date.Year == year));
            }
            var routes = await query
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
            if (!string.IsNullOrWhiteSpace(route.From))
            {
                placemark.ExtendedData.AddData(new Data { Name = "from", Value = route.From });
            }
            if (!string.IsNullOrWhiteSpace(route.To))
            {
                placemark.ExtendedData.AddData(new Data { Name = "to", Value = route.To });
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

        [HttpPut("editmultiple")]
        public async Task<ActionResult> UpdateMultipleRoutes([FromBody] EditMultiple editMultiple)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }

            var selectedRoutes = await _context.Routes
                .Include(r => r.RouteCountries)
                .Include(r => r.RouteMaps)
                .Where(r => editMultiple.RouteIds.Contains(r.RouteId) && r.RouteMaps.Any(rm => rm.Map.UserId == userIdClaim))
                .ToListAsync();

            if (editMultiple.UpdateDate)
            {
                selectedRoutes.ForEach(r => r.FirstDateTime = editMultiple.Date);
            }
            if (editMultiple.UpdateType)
            {
                var validType = await _context.RouteTypes.AnyAsync(t => t.TypeId == editMultiple.TypeId && t.UserId == userIdClaim);
                if (validType)
                    selectedRoutes.ForEach(r => r.RouteTypeId = editMultiple.TypeId);
            }

            if (editMultiple.UpdateCountries)
            {
                var validSelectedCountryIds = await _context.Countries.Where(c => editMultiple.Countries.Contains(c.CountryId) && c.UserId == userIdClaim).Select(c => c.CountryId).ToListAsync();

                selectedRoutes.ForEach(route =>
                {
                    var toDelete = new List<RouteCountry>();
                    var toAdd = new List<int>();
                    route.RouteCountries.ForEach(r =>
                    {
                        if (!validSelectedCountryIds.Contains(r.CountryId))
                        {
                            toDelete.Add(r);
                        }
                    });
                    if (validSelectedCountryIds != null)
                    {
                        validSelectedCountryIds.ForEach(rm1 =>
                        {
                            if (!route.RouteMaps.Select(rm => rm.MapId).Contains(rm1))
                            {
                                toAdd.Add(rm1);
                            }
                        });
                    }
                    if (toDelete.Any())
                    {
                        _context.RoutesCountries.RemoveRange(toDelete);
                    }

                    if (toAdd.Any())
                    {
                        _context.RoutesCountries.AddRange(toAdd.Select(c => new RouteCountry { CountryId = c, RouteId = route.RouteId }));
                    }

                });

            }

            if (editMultiple.UpdateMaps)
            {
                var validSelectedMapIds = await _context.Maps.Where(m => editMultiple.Maps.Contains(m.MapId) && m.UserId == userIdClaim).Select(m => m.MapId).ToListAsync();

                selectedRoutes.ForEach(route =>
                {
                    var toDelete = new List<RouteMap>();
                    var toAdd = new List<int>();
                    route.RouteMaps.ForEach(r =>
                    {
                        if (!editMultiple.Maps.Contains(r.MapId))
                        {
                            toDelete.Add(r);
                        }
                    });
                    if (editMultiple.Maps != null)
                    {
                        editMultiple.Maps.ForEach(rm1 =>
                        {
                            if (!route.RouteMaps.Select(rm => rm.MapId).Contains(rm1))
                            {
                                toAdd.Add(rm1);
                            }
                        });
                    }
                    if (toDelete.Any())
                    {
                        _context.RoutesMaps.RemoveRange(toDelete);
                    }

                    if (toAdd.Any())
                    {
                        _context.RoutesMaps.AddRange(toAdd.Select(m => new RouteMap { MapId = m, RouteId = route.RouteId }));
                    }

                });

            }


            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpGet("instances/{id}")]
        public async Task<ActionResult<Route>> GetRouteInstances(int id, [FromQuery] DateTime from, [FromQuery] DateTime to)
        {
            Claim claim = User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            var userIdClaim = int.Parse(claim != null ? claim.Value : "-1");
            var route = await _context.Routes
              .Where(r => r.RouteId == id)
              .Include(r => r.RouteType)
              .Include(r => r.RouteInstances)
              .ThenInclude(ri => ri.RouteInstanceProperties)
              .Include(r => r.RouteInstances)
              .ThenInclude(ri => ri.RouteInstanceMaps)
              .Where(r => r.RouteMaps.Any(rm => rm.Map.UserId == userIdClaim))
              .SingleOrDefaultAsync();
            if (route == null)
            {
                return NotFound();
            }
            route.RouteInstances = route.RouteInstances.OrderByDescending(ri => ri.Date).ToList();
            if (from != null && from != default && to != null && to != default)
            {
                route.RouteInstances = route.RouteInstances.Where(ri => ri.Date >= from && ri.Date < to).ToList();
            }

            return route;
        }

        [HttpGet("instances/{mapGuid}/{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<Route>> GetRouteInstances(Guid mapGuid, int id, [FromQuery] DateTime from, [FromQuery] DateTime to)
        {
            Claim claim = User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            var userIdClaim = int.Parse(claim != null ? claim.Value : "-1");
            var route = await _context.Routes
              .Where(r => r.RouteId == id)
              .Include(r => r.RouteType)
              .Include(r => r.RouteMaps)
              .ThenInclude(rm => rm.Map)
              .Include(r => r.RouteInstances)
              .ThenInclude(ri => ri.RouteInstanceProperties)
              .Include(r => r.RouteInstances)
              .ThenInclude(ri => ri.RouteInstanceMaps)
              .ThenInclude(rim => rim.Map)
              .Where(r => r.RouteMaps.Any(rm => !string.IsNullOrWhiteSpace(rm.Map.SharingLinkName) || rm.Map.UserId == userIdClaim))
              .SingleOrDefaultAsync();
            if (route == null)
            {
                return NotFound();
            }
            route.RouteInstances = route.RouteInstances.Where(ri => (ri.RouteInstanceMaps.Any(rim => rim.Map.MapGuid == mapGuid)
                                                            || ri.Route.RouteMaps.Any(rm => rm.Map.MapGuid == mapGuid))).ToList();
            route.RouteInstances = route.RouteInstances.OrderByDescending(ri => ri.Date).ToList();
            if (from != null && from != default && to != null && to != default)
            {
                route.RouteInstances = route.RouteInstances.Where(ri =>
                ri.Date >= from &&
                ri.Date < to).ToList();
            }


            return route;
        }

        [HttpPut("instances")]
        public async Task<ActionResult> UpdateInstance([FromBody] RouteInstanceUpdate update)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var route = await _context.Routes
                .Where(r => r.RouteId == update.RouteId && r.RouteMaps.Any(rm => rm.Map.UserId == userIdClaim))
                .Include(r => r.RouteInstances)
                .ThenInclude(ri => ri.RouteInstanceProperties)
                .Include(r => r.RouteInstances)
                .ThenInclude(ri => ri.RouteInstanceMaps)
                .SingleOrDefaultAsync();

            if (route == null)
            {
                return NotFound();
            }

            if (update.RouteInstanceId.HasValue)
            {



                var current = route.RouteInstances.SingleOrDefault(ri => ri.RouteInstanceId == update.RouteInstanceId);
                if (current != null)
                {
                    current.Date = update.Date;
                    var toDelete = current.RouteInstanceProperties.Where(ri => !update.RouteInstanceProperties.Any(uri => uri.RouteInstancePropertyId == ri.RouteInstancePropertyId)).ToList();
                    current.RouteInstanceProperties = current.RouteInstanceProperties.Where(ri => !toDelete.Any(dri => dri.RouteInstancePropertyId == ri.RouteInstancePropertyId)).ToList();

                    update.RouteInstanceProperties.ForEach(rip =>
                    {
                        if (rip.RouteInstancePropertyId.HasValue)
                        {
                            var currentProp = current.RouteInstanceProperties.Single(rp => rp.RouteInstancePropertyId == rip.RouteInstancePropertyId);
                            currentProp.Key = rip.Key;
                            currentProp.Bool = rip.Bool;
                            currentProp.Value = rip.Value;
                        }
                        else
                        {
                            current.RouteInstanceProperties.Add(new RouteInstanceProperty
                            {
                                Key = rip.Key,
                                Bool = rip.Bool,
                                Value = rip.Value
                            });
                        }
                    });

                    var mapsToDelete = current.RouteInstanceMaps.Where(ri => !update.RouteInstanceMaps.Any(uri => uri.MapId == ri.MapId)).ToList();
                    current.RouteInstanceMaps = current.RouteInstanceMaps.Where(ri => !mapsToDelete.Any(dri => dri.MapId == ri.MapId)).ToList();

                    var toAdd = update.RouteInstanceMaps.Where(ri => !current.RouteInstanceMaps.Any(uri => uri.MapId == ri.MapId)).ToList();
                    toAdd.ForEach(mapToAdd => current.RouteInstanceMaps.Add(new RouteInstanceMap { MapId = mapToAdd.MapId }));

                }
            }
            else
            {
                var newInstance = new RouteInstance
                {
                    Date = update.Date
                };
                newInstance.RouteInstanceProperties = new List<RouteInstanceProperty>();
                update.RouteInstanceProperties.ForEach(rip =>
                {
                    newInstance.RouteInstanceProperties.Add(
                        new RouteInstanceProperty
                        {
                            Key = rip.Key,
                            Bool = rip.Bool,
                            Value = rip.Value
                        });
                });
                route.RouteInstances.Add(newInstance);
            }
            await _context.SaveChangesAsync();

            return Ok();
        }


        [HttpDelete("instances/{id:int}")]
        public async Task<ActionResult> DeleteInstance(int id)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var routeInstance = await _context.RouteInstances
                .Where(r => r.RouteInstanceId == id && r.Route.RouteMaps.Any(rm => rm.Map.UserId == userIdClaim))
                .SingleOrDefaultAsync();

            if (routeInstance == null)
            {
                return NotFound();
            }

            _context.RouteInstances.Remove(routeInstance);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpGet("instances/tags/autocomplete")]
        public async Task<ActionResult> AutocompleteTags()
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var tags = await _context.RouteInstanceProperties
                .Where(rip => rip.RouteInstance.Route.RouteMaps.Any(rm => rm.Map.UserId == userIdClaim))
                .GroupBy(rip => rip.Key)
                .Select(a => new { a.Key, Count = a.Count() })
                .OrderByDescending(a => a.Count)
                .Select(a => a.Key)
                .ToListAsync();

            return Ok(tags);
        }

        [HttpPatch("{id:int}/assignRegions")]
        public async Task<IActionResult> AssignRegionsToRouteAsync(int id, [FromServices] IRouteRegionsService routeRegionsService)
        {
            var route = await _context.Routes.Include(r => r.Regions).SingleAsync(r => r.RouteId == id);
            await routeRegionsService.AssignRegionsToRouteAsync(route);
            await _context.SaveChangesAsync();
            return Ok();
        }

    }
}
