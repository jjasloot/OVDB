﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using OV_DB.Enum;
using OV_DB.Helpers;
using OV_DB.Models;
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

        public ImporterController(IMemoryCache memoryCache, OVDBDatabaseContext context, IConfiguration configuration)
        {
            _cache = memoryCache;
            _context = context;
            _configuration = configuration;
        }

        [HttpGet("find")]
        public async Task<ActionResult> GetLinesAsync([FromQuery]string reference, [FromQuery] OSMRouteType? routeType, [FromQuery]string network, [FromQuery] DateTime? dateTime)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return BadRequest();
            }
            var id = reference + "|" + routeType.ToString() + "|" + network + "|" + (dateTime.HasValue ? dateTime.Value.ToString("o") : "");
            var responseList = await _cache.GetOrCreateAsync(id, async i => await CreateCacheLines(reference, routeType, network, i, dateTime));
            if (responseList == null)
            {
                _cache.Remove(id);
                await Task.Delay(1000);
                responseList = await _cache.GetOrCreateAsync(id, async i => await CreateCacheLines(reference, routeType, network, i, dateTime));
            }
            return Ok(responseList);
        }
        [HttpGet("network")]
        public async Task<ActionResult> GetNetworkLinesAsync([FromQuery]string network, [FromQuery]DateTime? dateTime)
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
                await Task.Delay(1000);
                responseList = await _cache.GetOrCreateAsync(id, async i => await CreateCacheLinesNetwork(network, dateTime, i));
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
                _cache.Remove(id);
                await Task.Delay(1000);
                osm = await _cache.GetOrCreateAsync(idCache, async i => await CreateCache(id, i, dateTime));
            }
            var relation = osm.Elements.SingleOrDefault(e => e.Type == TypeEnum.Relation);
            var stops = new List<Element>();
            relation.Members.ForEach(way =>
            {
                if (way.Role.Contains("Platform", StringComparison.OrdinalIgnoreCase) || way.Role.Contains("Stop", StringComparison.OrdinalIgnoreCase))
                {
                    var stop = osm.Elements.SingleOrDefault(e => e.Id == way.Ref);
                    if (stop != null && stop.Lon != null && stop.Lat != null)
                    {
                        var currentName = stop.Tags.GetValueOrDefault("name");

                        if (!string.IsNullOrWhiteSpace(currentName) && !stops.Any(s =>
                           (s.Tags.GetValueOrDefault("name").Contains(currentName, StringComparison.OrdinalIgnoreCase) ||
                           currentName.Contains(s.Tags.GetValueOrDefault("name"), StringComparison.OrdinalIgnoreCase))
                           && s.Tags.GetValueOrDefault("public_transport", "") != stop.Tags.GetValueOrDefault("public_transport", "")))
                            stops.Add(stop);
                    }
                }

            });
            var listOfStops = stops.Select(s => new OSMLineStop
            {
                Id = s.Id,
                Name = s.Tags.GetValueOrDefault("name"),
                Ref = s.Tags.GetValueOrDefault("ref")
            }).ToList();
            return Ok(listOfStops);
        }

        private async Task<string> GetRelationFromOSMAsync(int id, DateTime? dateTime)
        {
            var query = $"<osm-script output=\"json\"";
            if (dateTime != null)
                query += $" date=\"{dateTime:o}\"";
            query += $"><id-query type=\"relation\" ref=\"" + id + $"\"/>  <union into=\"_\">    <item from=\"_\" into=\"_\"/>    <recurse from=\"_\" into=\"_\" type=\"down\"/>  </union>  <print from=\"_\" /></osm-script>";

            string text = null;
            var userAgent = _configuration.GetValue<string>("UserAgent", "OVDB");
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);

                var response = await httpClient.PostAsync("https://overpass-api.de/api/interpreter", new StringContent(query));
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    return null;
                }
                text = await response.Content.ReadAsStringAsync();
            }

            return text;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult> Read(int id, [FromQuery] long from, [FromQuery] long to, [FromQuery] DateTime? dateTime)
        {
            var idCache = id + "|" + (dateTime.HasValue ? dateTime.Value.ToString("o") : "");

            var osm = await _cache.GetOrCreateAsync(idCache, async i => await CreateCache(id, i, dateTime));
            if (osm == null)
            {
                _cache.Remove(id);
                await Task.Delay(1000);
                osm = await _cache.GetOrCreateAsync(idCache, async i => await CreateCache(id, i, dateTime));
            }
            var relation = osm.Elements.SingleOrDefault(e => e.Type == TypeEnum.Relation);
            var stops = new List<Element>();
            var lists = new List<List<IPosition>>();
            relation.Members.ForEach(way =>
            {
                if (way.Role.Contains("Platform", StringComparison.OrdinalIgnoreCase) || way.Role.Contains("Stop", StringComparison.OrdinalIgnoreCase))
                {
                    var stop = osm.Elements.SingleOrDefault(e => e.Id == way.Ref);
                    if (stop != null && stop.Lon != null && stop.Lat != null)
                    {
                        var currentName = stop.Tags.GetValueOrDefault("name");

                        if (!string.IsNullOrWhiteSpace(currentName) && !stops.Any(s =>
                           (s.Tags.GetValueOrDefault("name").Contains(currentName, StringComparison.OrdinalIgnoreCase) ||
                           currentName.Contains(s.Tags.GetValueOrDefault("name"), StringComparison.OrdinalIgnoreCase))
                           && s.Tags.GetValueOrDefault("public_transport", "") != stop.Tags.GetValueOrDefault("public_transport", "")))
                            stops.Add(stop);
                    }
                }
                else
                {
                    var nodes = osm.Elements.Single(e => e.Id == way.Ref).Nodes;
                    var subList = new List<IPosition>();
                    if (nodes != null)
                    {
                        nodes.ForEach(n =>
                        {
                            var node = osm.Elements.Single(e => e.Id == n);
                            subList.Add(new Position(node.Lat.GetValueOrDefault(), node.Lon.GetValueOrDefault()));

                        });
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
            SortListOfList(lists);
            var oneList = lists.SelectMany(i => i).ToList();
            var pointts2 = oneList.Select(l => l.Longitude.ToString(CultureInfo.InvariantCulture) + ", " + l.Latitude.ToString(CultureInfo.InvariantCulture)).ToList();
            var pointts2String = string.Join("\n", pointts2);

            var fromStop = stops.SingleOrDefault(s => s.Id == from);
            var toStop = stops.SingleOrDefault(s => s.Id == to);
            if (fromStop == null || toStop == null)
            {
                element.GeoJson = CoordsToGeoJson(oneList);
                return Ok(element);
            }
            var min = double.MaxValue;
            IPosition minPosition = null;

            oneList.ToList().ForEach(s =>
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

            oneList.ToList().ForEach(s =>
            {
                var distance = GeometryHelper.distance(s.Latitude, s.Longitude, toStop.Lat.Value, toStop.Lon.Value, 'k');
                if (distance < toMin)
                {
                    toMin = distance;
                    toMinPosition = s;
                }
            });


            var startIndex = oneList.ToList().IndexOf(minPosition);
            var toIndex = oneList.ToList().IndexOf(toMinPosition);

            if (fromStop.Id == stops.First().Id)
                startIndex = 0;

            if (toStop.Id == stops.Last().Id)
                toIndex = oneList.Count - 1;

            if (toIndex == startIndex)
            {
                return Ok(element);
            }
            var filteredList = oneList.Take(toIndex + 1).Skip(startIndex).ToList();
            var filteredPoints = filteredList.Select(l => l.Longitude.ToString(CultureInfo.InvariantCulture) + ", " + l.Latitude.ToString(CultureInfo.InvariantCulture)).ToList();
            var filteredPointsList = string.Join("\n", filteredPoints);
            element.GeoJson = CoordsToGeoJson(filteredList);
            return Ok(element);
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



        private void SortListOfList(List<List<IPosition>> test)
        {


            if (test[1].Select(l => l.Latitude).Contains(test[0].First().Latitude) && test[1].Select(l => l.Longitude).Contains(test[0].First().Longitude))
            {
                test[0].Reverse();
            }

            for (int index = 1; index < test.Count; index++)
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
                        if (startIndex > endIndex)
                        {
                            points.AddRange(test[index].Skip(startIndex));
                            points.AddRange(test[index].Take(endIndex));
                        }
                        else
                        {
                            points.AddRange(test[index].Take(endIndex).Skip(startIndex));
                        }
                        test[index] = points;
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

        }

        private async Task<OSM> CreateCache(int id, ICacheEntry entry, DateTime? dateTime)
        {
            entry.SetSlidingExpiration(TimeSpan.FromMinutes(15));
            var text = await GetRelationFromOSMAsync(id, dateTime);
            if (text == null)
            {
                return null;
            }
            var osm = JsonConvert.DeserializeObject<OSM>(text.ToString());
            return osm;
        }

        private async Task<List<OSMLineDTO>> CreateCacheLines(string reference, OSMRouteType? routeType, string network, ICacheEntry entry, DateTime? dateTime)
        {
            entry.SetSlidingExpiration(TimeSpan.FromMinutes(15));
            return await CreateRoutesListAsync(reference, routeType, network, dateTime);
        }

        private async Task<List<OSMLineDTO>> CreateCacheLinesNetwork(string network, DateTime? dateTime, ICacheEntry entry)
        {
            entry.SetSlidingExpiration(TimeSpan.FromMinutes(15));
            return await CreateNetworkRoutesListAsync(network, dateTime);
        }

        private async Task<List<OSMLineDTO>> CreateNetworkRoutesListAsync(string network, DateTime? dateTime)
        {
            var query = $"<osm-script output=\"json\"><query type=\"relation\">";
            if (dateTime.HasValue)
                query = $"<osm-script output=\"json\" date=\"{dateTime:o}\"><query type=\"relation\">";
            query += $"<has-kv k=\"network\" v=\"" + network + "\"/>";
            query += $"<has-kv k=\"route\"/>";
            query += $"</query><print mode=\"tags\" order=\"quadtile\"/></osm-script>";
            string text = null;
            var userAgent = _configuration.GetValue<string>("UserAgent", "OVDB");
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
                var response = await httpClient.PostAsync("https://overpass-api.de/api/interpreter", new StringContent(query));
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    return null;
                }
                text = await response.Content.ReadAsStringAsync();
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

        private static async Task<List<OSMLineDTO>> CreateRoutesListAsync(string reference, OSMRouteType? routeType, string network, DateTime? dateTime)
        {
            var query = $"<osm-script output=\"json\"><query type=\"relation\">";
            if (dateTime.HasValue)
            {
                query = $"<osm-script output=\"json\" date=\"{dateTime.Value.ToUniversalTime():o}\"><query type=\"relation\">";
            }
            query += $"<has-kv k=\"ref\" v=\"" + reference + "\"/>";
            if (routeType != null && routeType != OSMRouteType.not_specified)
            {
                query += $"<has-kv k=\"route\" v=\"" + routeType.ToString() + "\"/>";
            }
            if (!string.IsNullOrWhiteSpace(network))
            {
                query += $"<has-kv k=\"network\" v=\"" + network + "\"/>";
            }
            query += $"</query><print mode=\"tags\" geometry=\"center\" order=\"quadtile\"/></osm-script>";
            string text = null;
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "OVDB/1.0 (contact-me jaapslootbeek@gmail.com)");
                try
                {
                    var response = await httpClient.PostAsync("https://overpass-api.de/api/interpreter", new StringContent(query));

                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        return null;
                    }
                    text = await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    throw;
                }
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


            var route = new Route
            {
                Coordinates = string.Join('\n', coordinates),
                Name = line.Name,
                RouteMaps = new List<RouteMap>()
            };
            if (route.Name == null)
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

            var maps = await _context.Maps.Where(m => m.UserId == userIdClaim).ToListAsync();

            var defaultMap = maps.Where(m => m.Default == true).FirstOrDefault();
            if (defaultMap == null)
            {
                defaultMap = maps.First();
            }

            route.RouteMaps.Add(new RouteMap { MapId = defaultMap.MapId });


            _context.Routes.Add(route);
            await _context.SaveChangesAsync();
            return Ok(route);
        }
    }
}