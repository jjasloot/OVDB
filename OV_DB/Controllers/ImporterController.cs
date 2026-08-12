using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Security.Claims;
using System.Threading.Tasks;
using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OV_DB.Enum;
using OV_DB.Helpers;
using OV_DB.Models;
using OV_DB.Services;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ImporterController : ControllerBase
    {
        private IMemoryCache _cache;
        private OVDBDatabaseContext _context;
        private IConfiguration _configuration;
        private readonly IRouteRegionsService _routeRegionsService;
        private readonly IOverpassService _overpassService;
        private readonly ILogger<ImporterController> _logger;

        public ImporterController([FromKeyedServices("OsmCache")] IMemoryCache memoryCache, OVDBDatabaseContext context, IConfiguration configuration, IRouteRegionsService routeRegionsService, IOverpassService overpassService, ILogger<ImporterController> logger)
        {
            _cache = memoryCache;
            _context = context;
            _configuration = configuration;
            _routeRegionsService = routeRegionsService;
            _overpassService = overpassService;
            _logger = logger;
        }

        [HttpGet("find")]
        public async Task<ActionResult> GetLinesAsync([FromQuery] string reference, [FromQuery] OSMRouteType? routeType, [FromQuery] string network, [FromQuery] DateTime? dateTime)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return BadRequest();
            }
            var id = reference + "|" + routeType.ToString() + "|" + network + "|" + (dateTime.HasValue ? dateTime.Value.ToString("o") : "");
            var responseList = await _cache.GetOrCreateAsync(id, async i => await CreateCacheLines(reference, routeType, network, i, dateTime));
            if (responseList == null)
            {
                // Don't cache the failure; the Overpass service already tried every mirror.
                _cache.Remove(id);
                return StatusCode(502, "OpenStreetMap could not be reached, please try again later");
            }
            return Ok(responseList);
        }
        [HttpGet("network")]
        public async Task<ActionResult> GetNetworkLinesAsync([FromQuery] string network, [FromQuery] DateTime? dateTime)
        {
            if (string.IsNullOrWhiteSpace(network))
            {
                return BadRequest();
            }
            var id = "network" + "|" + network + "|" + (dateTime.HasValue ? dateTime.Value.ToString("o") : "");
            var responseList = await _cache.GetOrCreateAsync(id, async i => await CreateCacheLinesNetwork(network, dateTime, i));
            if (responseList == null)
            {
                _cache.Remove(id);
                return StatusCode(502, "OpenStreetMap could not be reached, please try again later");
            }
            return Ok(responseList);
        }


        [HttpGet("{id:int}/stops")]
        public async Task<ActionResult<List<OSMLineStop>>> GetStops(int id, [FromQuery] DateTime? dateTime)
        {

            var idCache = id + "|" + (dateTime.HasValue ? dateTime.Value.ToString("o") : "");
            var osm = await _cache.GetOrCreateAsync(idCache, async i => await CreateCache(id, i, dateTime));
            if (osm == null)
            {
                _cache.Remove(idCache);
                return StatusCode(502, "OpenStreetMap could not be reached, please try again later");
            }
            var relation = osm.Elements.SingleOrDefault(e => e.Type == TypeEnum.Relation);
            if (relation == null)
            {
                return NotFound();
            }
            var elementsById = osm.Elements.ToLookup(e => e.Id);
            var stops = new List<Element>();
            relation.Members.ForEach(way =>
            {
                if (way.Role.Contains("Platform", StringComparison.OrdinalIgnoreCase) || way.Role.Contains("Stop", StringComparison.OrdinalIgnoreCase))
                {
                    HandleNewStop(way, elementsById, stops);
                }

            });
            var listOfStops = stops.Select(s => new OSMLineStop
            {
                Id = s.Id,
                Name = s.Tags.GetValueOrDefault("friendlyName"),
                Ref = s.Tags.GetValueOrDefault("ref")
            }).ToList();
            return Ok(listOfStops);
        }

        private static void AddRelevantStops(List<Element> stops, Element stop)
        {
            var currentName = stop.Tags.GetValueOrDefault("name");
            var previousStop = stops.LastOrDefault();

            if (!string.IsNullOrWhiteSpace(currentName) && !(
                previousStop != null &&
               (previousStop.Tags.GetValueOrDefault("name").Contains(currentName, StringComparison.OrdinalIgnoreCase) ||
               currentName.Contains(previousStop.Tags.GetValueOrDefault("name"), StringComparison.OrdinalIgnoreCase))
               && previousStop.Tags.GetValueOrDefault("public_transport", "") != stop.Tags.GetValueOrDefault("public_transport", "")))
                stops.Add(stop);
        }

        private async Task<string> GetRelationFromOSMAsync(int id, DateTime? dateTime)
        {
            var query = $"[out:json][timeout:120]";
            if (dateTime != null)
                query += $"[date:\"{dateTime.Value.ToUniversalTime():o}\"]";
            query += $";relation({id});";
            query += "(._;>;);out;node(r)->.nodes;.nodes is_in;area._[boundary=administrative][admin_level=10]->.areas;foreach.areas -> .a {  node.nodes(area.a);  convert node ::id = id(), is_in= a.set(t[\"name\"]);  out; }";
            return await _overpassService.QueryAsync(query);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult> Read(int id, [FromQuery] long from, [FromQuery] long to, [FromQuery] DateTime? dateTime)
        {
            var idCache = id + "|" + (dateTime.HasValue ? dateTime.Value.ToString("o") : "");

            var osm = await _cache.GetOrCreateAsync(idCache, async i => await CreateCache(id, i, dateTime));
            if (osm == null)
            {
                _cache.Remove(idCache);
                return StatusCode(502, "OpenStreetMap could not be reached, please try again later");
            }

            var relation = osm.Elements.SingleOrDefault(e => e.Type == TypeEnum.Relation);
            if (relation == null)
            {
                return NotFound();
            }
            var elementsById = osm.Elements.ToLookup(e => e.Id);
            var stops = new List<Element>();
            var lists = new List<List<IPosition>>();
            relation.Members.ForEach(way =>
            {
                if (way.Role.Contains("Platform", StringComparison.OrdinalIgnoreCase) || way.Role.Contains("Stop", StringComparison.OrdinalIgnoreCase))
                {
                    HandleNewStop(way, elementsById, stops);
                }
                else
                {
                    var nodes = elementsById[way.Ref].FirstOrDefault()?.Nodes;
                    var subList = new List<IPosition>();
                    if (nodes != null)
                    {
                        nodes.ForEach(n =>
                        {
                            var node = elementsById[n].FirstOrDefault();
                            subList.Add(new Position(node.Lat.GetValueOrDefault(), node.Lon.GetValueOrDefault()));

                        });
                        if (nodes.Any())
                            lists.Add(subList);
                    }
                }
            });
            var element = new OSMLineDTO();
            if (relation.Tags.ContainsKey("name"))
                element.Name = relation.Tags["name"];
            if (relation.Tags.ContainsKey("description"))
                element.Description = relation.Tags["description"];
            if (relation.Tags.ContainsKey("from"))
                element.From = relation.Tags["from"];
            element.Id = relation.Id;
            if (relation.Tags.ContainsKey("network"))
                element.Network = relation.Tags["network"];
            if (relation.Tags.ContainsKey("operator"))
                element.Operator = relation.Tags["operator"];
            if (relation.Tags.ContainsKey("to"))
                element.To = relation.Tags["to"];
            if (relation.Tags.ContainsKey("ref"))
                element.Ref = relation.Tags["ref"];
            if (relation.Tags.ContainsKey("fixme"))
                element.PotentialErrors = relation.Tags["fixme"];
            if (relation.Tags.ContainsKey("colour"))
                element.Colour = relation.Tags["colour"];
            if (relation.Tags.ContainsKey("ref") && relation.Tags.ContainsKey("from") && relation.Tags.ContainsKey("to"))
            {
                element.Name = relation.Tags["ref"] + ": " + relation.Tags["from"] + " => " + relation.Tags["to"];
            }
            lists = SortListOfList(lists);
            var oneList = lists.SelectMany(i => i).ToList();
            var pointts2 = oneList.Select(l => l.Longitude.ToString(CultureInfo.InvariantCulture) + ", " + l.Latitude.ToString(CultureInfo.InvariantCulture)).ToList();
            var pointts2String = string.Join("\n", pointts2);

            var fromStop = stops.FirstOrDefault(s => s.Id == from);
            var toStop = stops.LastOrDefault(s => s.Id == to);
            if (fromStop == null || toStop == null)
            {
                element.GeoJson = CoordsToGeoJson(oneList);
                return Ok(element);
            }
            if (relation.Tags.ContainsKey("ref"))
            {
                element.Name = relation.Tags["ref"] + ": " + fromStop.Tags["friendlyName"] + " => " + toStop.Tags["friendlyName"];
            }
            var min = double.MaxValue;
            IPosition minPosition = null;

            oneList.Take(oneList.Count - 20).ToList().ForEach(s =>
            {
                var distance = GeometryHelper.distance(s.Latitude, s.Longitude, fromStop.Lat.Value, fromStop.Lon.Value, 'k');
                if (distance < min)
                {
                    min = distance;
                    minPosition = s;
                }
            });
            var toMin = double.MaxValue;
            IPosition toMinPosition = null;

            var reversedList = oneList.ToList();
            reversedList.Reverse();

            reversedList.ForEach(s =>
            {
                var distance = GeometryHelper.distance(s.Latitude, s.Longitude, toStop.Lat.Value, toStop.Lon.Value, 'k');
                if (distance < toMin)
                {
                    toMin = distance;
                    toMinPosition = s;
                }
            });


            var startIndex = oneList.IndexOf(minPosition);
            var toIndex = oneList.LastIndexOf(toMinPosition);

            if (fromStop.Id == stops.First().Id && startIndex < 20)
                startIndex = 0;

            if (toStop.Id == stops.Last().Id && toIndex > oneList.Count - 20)
                toIndex = oneList.Count - 1;

            if (toIndex == startIndex)
            {
                return Ok(element);
            }
            var reverseFromToOrder = false;
            if (startIndex > toIndex)
            {
                var temp = startIndex;
                startIndex = toIndex;
                toIndex = temp;
                reverseFromToOrder = true;

                if (relation.Tags.ContainsKey("ref"))
                {
                    element.Name = relation.Tags["ref"] + ": " + toStop.Tags["friendlyName"] + " => " + fromStop.Tags["friendlyName"];
                }
            }


            var filteredList = oneList.Take(toIndex + 1).Skip(startIndex).ToList();
            var filteredPoints = filteredList.Select(l => l.Longitude.ToString(CultureInfo.InvariantCulture) + ", " + l.Latitude.ToString(CultureInfo.InvariantCulture)).ToList();
            var filteredPointsList = string.Join("\n", filteredPoints);
            element.GeoJson = CoordsToGeoJson(filteredList);
            if (reverseFromToOrder)
            {
                element.From = toStop.Tags["friendlyName"];
                element.To = fromStop.Tags["friendlyName"];
            }
            else
            {
                element.From = fromStop.Tags["friendlyName"];
                element.To = toStop.Tags["friendlyName"];
            }
            return Ok(element);
        }

        private static void HandleNewStop(Member way, ILookup<long, Element> elementsById, List<Element> stops)
        {
            var bothStops = elementsById[way.Ref].ToList();
            var stop = bothStops.FirstOrDefault(s => s.Lat.HasValue);
            if (stop == null)
            {
                return;
            }
            bothStops.Remove(stop);
            var currentName = "";
            if (stop.Tags == null)
            {
                stop.Tags = new Dictionary<string, string>();
            }
            if (stop.Tags.ContainsKey("name"))
            {
                currentName = stop.Tags["name"];
            }
            var cityElement = bothStops.Where(s => s.Tags.ContainsKey("is_in")).FirstOrDefault();
            if (cityElement != null)
            {
                var city = cityElement.Tags["is_in"];
                if (!currentName.Contains(city))
                {
                    currentName = city + ", " + currentName;
                }
            }
            if (stop.Tags.ContainsKey("friendlyName"))
            {
                stop.Tags["friendlyName"] = currentName;
            }
            else
            {
                stop.Tags.Add("friendlyName", currentName);
            }
            if (stop != null && stop.Lon != null && stop.Lat != null)
            {
                AddRelevantStops(stops, stop);
            }
        }

        private FeatureCollection CoordsToGeoJson(List<IPosition> coordinates)
        {
            if (!coordinates.Any())
            {
                return null;
            }
            var coords = coordinates.Select(r => new Position(r.Latitude, r.Longitude)).ToList();
            var geo = new GeoJSON.Net.Geometry.LineString(coords);

            var feature = new GeoJSON.Net.Feature.Feature(geo);
            var featureCollection = new FeatureCollection(new List<Feature> { feature });

            return featureCollection;
        }



        private List<List<IPosition>> SortListOfList(List<List<IPosition>> test)
        {
            if (test.Count < 2)
            {
                return test;
            }
            test = test.Where(t => t.Count > 0).ToList();


            if (test[1].Select(l => l.Latitude).Contains(test[0].First().Latitude) && test[1].Select(l => l.Longitude).Contains(test[0].First().Longitude))
            {
                test[0].Reverse();
            }

            for (int index = 1; index < test.Count; index++)
            {
                try
                {
                    if (index < test.Count - 1 && test[index].First().Latitude == test[index].Last().Latitude && test[index].First().Longitude == test[index].Last().Longitude)
                    {
                        //Roundabout

                        var entryLocation = test[index - 1].Last();
                        var startIndex = test[index].FindIndex(t => t.Longitude == entryLocation.Longitude && t.Latitude == entryLocation.Latitude);


                        var exitLocation = test[index + 1].First();
                        var endIndex = test[index].FindIndex(t => t.Longitude == exitLocation.Longitude && t.Latitude == exitLocation.Latitude);

                        if (endIndex < 0)
                        {
                            exitLocation = test[index + 1].Last();
                            endIndex = test[index].FindIndex(t => t.Longitude == exitLocation.Longitude && t.Latitude == exitLocation.Latitude);
                        }

                        if (startIndex >= 0 && endIndex >= 0)
                        {
                            var points = new List<IPosition>();
                            if (startIndex >= endIndex)
                            {
                                points.AddRange(test[index].Skip(startIndex));
                                points.AddRange(test[index].Take(endIndex + 1));
                            }
                            else
                            {
                                points.AddRange(test[index].Take(endIndex + 1).Skip(startIndex));
                            }
                            test[index] = points;
                            if (!points.Any())
                            {
                                test.RemoveAt(index);
                                index--;
                            }
                        }

                    }
                    if (test[index - 1].Last().Latitude == test[index].Last().Latitude && test[index - 1].Last().Longitude == test[index].Last().Longitude)
                    {
                        test[index].Reverse();
                    }
                    else
                    {
                        if (test[index - 1].Last().Latitude == test[index].First().Latitude && test[index - 1].Last().Longitude == test[index].First().Longitude)
                        {
                            //correct direction
                        }
                        else
                        {
                            //We have to guess
                            var distanceStart = GeometryHelper.distance(test[index - 1].Last().Latitude, test[index - 1].Last().Longitude, test[index].First().Latitude, test[index].First().Longitude, 'k');
                            var distanceEnd = GeometryHelper.distance(test[index - 1].Last().Latitude, test[index - 1].Last().Longitude, test[index].Last().Latitude, test[index].Last().Longitude, 'k');

                            if (distanceEnd < distanceStart)
                            {
                                test[index].Reverse();
                            }

                        }
                    }
                }
                catch (Exception ex)
                {
                    // Was silently swallowed ("Hier") — surface it so mis-ordered geometry is at least traceable.
                    _logger.LogWarning(ex, "Failed to stitch way segment at index {Index} while ordering route geometry", index);
                }
            }
            return test;
        }

        private async Task<OSM> CreateCache(int id, ICacheEntry entry, DateTime? dateTime)
        {
            entry.SetSlidingExpiration(TimeSpan.FromMinutes(15));
            entry.SetSize(1);
            var text = await GetRelationFromOSMAsync(id, dateTime);
            if (text == null)
            {
                return null;
            }
            var osm = JsonConvert.DeserializeObject<OSM>(text.ToString());
            // Size by element count so a few huge international routes can't pin
            // hundreds of megabytes; the cache evicts older entries past its limit.
            entry.SetSize(osm?.Elements?.Count ?? 1);
            return osm;
        }

        private async Task<List<OSMLineDTO>> CreateCacheLines(string reference, OSMRouteType? routeType, string network, ICacheEntry entry, DateTime? dateTime)
        {
            entry.SetSlidingExpiration(TimeSpan.FromMinutes(15));
            entry.SetSize(1);
            var lines = await CreateRoutesListAsync(reference, routeType, network, dateTime);
            entry.SetSize(lines?.Count ?? 1);
            return lines;
        }

        private async Task<List<OSMLineDTO>> CreateCacheLinesNetwork(string network, DateTime? dateTime, ICacheEntry entry)
        {
            entry.SetSlidingExpiration(TimeSpan.FromMinutes(15));
            entry.SetSize(1);
            var lines = await CreateNetworkRoutesListAsync(network, dateTime);
            entry.SetSize(lines?.Count ?? 1);
            return lines;
        }

        private async Task<List<OSMLineDTO>> CreateNetworkRoutesListAsync(string network, DateTime? dateTime)
        {
            var query = $"<osm-script output=\"json\" timeout=\"60\"><query type=\"relation\">";
            if (dateTime.HasValue)
                query = $"<osm-script output=\"json\" timeout=\"60\" date=\"{dateTime.Value.ToUniversalTime():o}\"><query type=\"relation\">";
            query += $"<has-kv k=\"network\" modv=\"\" regv=\"" + SecurityElement.Escape(network) + "\"/>";
            query += $"<has-kv k=\"route\"/>";
            query += $"</query><print mode=\"tags\" order=\"quadtile\"/></osm-script>";
            var text = await _overpassService.QueryAsync(query);
            if (text == null)
            {
                return null;
            }

            var lines = JsonConvert.DeserializeObject<OsmLinesList>(text);
            var responseList = new List<OSMLineDTO>();

            lines.Elements = lines.Elements.OrderBy(e => { if (e.Tags.ContainsKey("name")) return e.Tags["name"]; return ""; }).ToList();


            lines.Elements.ForEach(l =>
            {
                var element = new OSMLineDTO();
                if (l.Tags.ContainsKey("name"))
                    element.Name = l.Tags["name"];
                if (l.Tags.ContainsKey("description"))
                    element.Description = l.Tags["description"];
                if (l.Tags.ContainsKey("from"))
                    element.From = l.Tags["from"];
                element.Id = l.Id;
                if (l.Tags.ContainsKey("network"))
                    element.Network = l.Tags["network"];
                if (l.Tags.ContainsKey("operator"))
                    element.Operator = l.Tags["operator"];
                if (l.Tags.ContainsKey("to"))
                    element.To = l.Tags["to"];
                if (l.Tags.ContainsKey("fixme"))
                    element.PotentialErrors = l.Tags["fixme"];
                if (l.Tags.ContainsKey("colour"))
                    element.Colour = l.Tags["colour"];
                responseList.Add(element);
            });
            return responseList; ;
        }

        private async Task<List<OSMLineDTO>> CreateRoutesListAsync(string reference, OSMRouteType? routeType, string network, DateTime? dateTime)
        {
            var query = $"<osm-script output=\"json\" timeout=\"60\"><query type=\"relation\">";
            if (dateTime.HasValue)
            {
                query = $"<osm-script output=\"json\" timeout=\"60\" date=\"{dateTime.Value.ToUniversalTime():o}\"><query type=\"relation\">";
            }
            query += $"<has-kv k=\"ref\" v=\"" + SecurityElement.Escape(reference) + "\"/>";
            if (routeType != null && routeType != OSMRouteType.not_specified)
            {
                query += $"<has-kv k=\"route\" v=\"" + SecurityElement.Escape(routeType.ToString()) + "\"/>";
            }
            if (!string.IsNullOrWhiteSpace(network))
            {
                query += $"<has-kv k=\"network\" modv=\"\" regv=\"" + SecurityElement.Escape(network) + "\"/>";
            }
            query += $"</query><print mode=\"tags\" geometry=\"center\" order=\"quadtile\"/></osm-script>";
            var text = await _overpassService.QueryAsync(query);
            if (text == null)
            {
                return null;
            }

            var lines = JsonConvert.DeserializeObject<OsmLinesList>(text);
            var responseList = new List<OSMLineDTO>();

            lines.Elements = lines.Elements.OrderBy(e =>
            {
                if (e.Center == null)
                {
                    return double.MaxValue / 10;
                }
                return GeometryHelper.distance(e.Center.Lat, e.Center.Lon, 52.228936, 5.321492, 'k');
            }).ToList();


            lines.Elements.ForEach(l =>
            {
                var element = new OSMLineDTO();
                if (l.Tags.ContainsKey("name"))
                    element.Name = l.Tags["name"];
                if (l.Tags.ContainsKey("description"))
                    element.Description = l.Tags["description"];
                if (l.Tags.ContainsKey("from"))
                    element.From = l.Tags["from"];
                element.Id = l.Id;
                if (l.Tags.ContainsKey("network"))
                    element.Network = l.Tags["network"];
                if (l.Tags.ContainsKey("operator"))
                    element.Operator = l.Tags["operator"];
                if (l.Tags.ContainsKey("to"))
                    element.To = l.Tags["to"];
                if (l.Tags.ContainsKey("fixme"))
                    element.PotentialErrors = l.Tags["fixme"];
                if (l.Tags.ContainsKey("colour"))
                    element.Colour = l.Tags["colour"];
                responseList.Add(element);
            });
            return responseList;
        }


        [HttpPost("addRoute")]
        public async Task<ActionResult<Route>> ImportRouteToDatabaseAsync([FromBody] OSMLineDTO line)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }

            var geojson = line.GeoJson;
            var coordinates =
                ((LineString)geojson.Features.Single().Geometry).Coordinates
                .Select(c => c.Longitude.ToString(CultureInfo.InvariantCulture) + ", " + c.Latitude.ToString(CultureInfo.InvariantCulture))
                .ToList();


            var lineString = new NetTopologySuite.Geometries.LineString(
                              ((LineString)geojson.Features.Single().Geometry).Coordinates.Select(c => new NetTopologySuite.Geometries.Coordinate(c.Longitude, c.Latitude)).ToArray()
                           );


            var route = new Route
            {
                LineString = lineString,
                Name = line.Name,
                Share = Guid.NewGuid(),
                RouteMaps = [],
                Regions = []
            };
            if (string.IsNullOrWhiteSpace(route.Name))
                route.Name = "Name";

            if (!string.IsNullOrWhiteSpace(line.Description))
            {
                route.Description = line.Description;
            }
            if (!string.IsNullOrWhiteSpace(line.Colour))
            {
                route.OverrideColour = line.Colour;
            }
            if (!string.IsNullOrWhiteSpace(line.Operator))
            {
                route.OperatingCompany = line.Operator;
            }
            if (!string.IsNullOrWhiteSpace(line.Ref))
            {
                route.LineNumber = line.Ref;
            }
            if (!string.IsNullOrWhiteSpace(line.From))
            {
                route.From = line.From;
            }
            if (!string.IsNullOrWhiteSpace(line.To))
            {
                route.To = line.To;
            }
            var maps = await _context.Maps.Where(m => m.UserId == userIdClaim).ToListAsync();

            var defaultMap = maps.Where(m => m.Default == true).FirstOrDefault();
            if (defaultMap == null)
            {
                defaultMap = maps.First();
            }

            route.RouteMaps.Add(new RouteMap { MapId = defaultMap.MapId });
            DistanceCalculationHelper.ComputeDistance(route);
            await _routeRegionsService.AssignRegionsToRouteAsync(route);

            _context.Routes.Add(route);
            await _context.SaveChangesAsync();
            return Ok(route);
        }
    }
}