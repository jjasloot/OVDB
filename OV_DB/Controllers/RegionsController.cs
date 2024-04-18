using AutoMapper;
using AutoMapper.QueryableExtensions;
using GeoJSON.Net.Geometry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Newtonsoft.Json;
using NuGet.Packaging;
using OV_DB.Models;
using OV_DB.Services;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RegionsController(IMemoryCache memoryCache, OVDBDatabaseContext context, IConfiguration configuration, IRouteRegionsService routeRegionsService, IMapper mapper) : ControllerBase
    {
        private IMemoryCache _cache = memoryCache;
        private OVDBDatabaseContext _context = context;
        private IConfiguration _configuration = configuration;

        private async Task<string> GetPolygonAsync(long id)
        {
            var query = $"[out:json]";
            query += $";relation({id});";
            query += "._;>;out;";
            string text = null;
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "OVDB");

                var response = await httpClient.GetAsync($"https://polygons.openstreetmap.fr/get_geojson.py?id={id}&params=0");
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    return null;
                }
                text = await response.Content.ReadAsStringAsync();
            }

            return text;
        }

        private static async Task<Dictionary<string, string>> GetTagsAsync(long id)
        {
            var query = $"[out:json]";
            query += $";relation({id});";
            query += "out tags;";
            string text = null;
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "OVDB");

                var response = await httpClient.PostAsync("https://overpass-api.de/api/interpreter", new StringContent(query));
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    return null;
                }
                text = await response.Content.ReadAsStringAsync();
            }
            var parsed = JsonConvert.DeserializeObject<OSM>(text.ToString());
            return parsed.Elements.Single().Tags;
        }

        [HttpPost]
        public async Task<ActionResult> CreateNew(NewRegion newRegion)
        {
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
            var tags = await GetTagsAsync(newRegion.OsmRelationId);

            var name = tags.GetValueOrDefault("name:en");
            if (string.IsNullOrWhiteSpace(name))
            {
                name = tags.GetValueOrDefault("name");
            }
            var nameNL = tags.GetValueOrDefault("name:nl");
            if (string.IsNullOrWhiteSpace(nameNL))
            {
                nameNL = tags.GetValueOrDefault("name");
            }
            var originalName = tags.GetValueOrDefault("name");

            var text = await GetPolygonAsync(newRegion.OsmRelationId);
            if (text == null)
            {
                return NotFound();
            }

            var serializer = GeoJsonSerializer.Create();
            using (var stringReader = new StringReader(text))
            using (var jsonReader = new JsonTextReader(stringReader))
            {
                var geometry = serializer.Deserialize<NetTopologySuite.Geometries.MultiPolygon>(jsonReader);

                var multiPolygon = GenerateCorrectMultiPolygon(geometry, []);
                var isValidOp = new NetTopologySuite.Operation.Valid.IsValidOp(multiPolygon);
                if (!isValidOp.IsValid)
                {
                    if (isValidOp.ValidationError.ErrorType is NetTopologySuite.Operation.Valid.TopologyValidationErrors.SelfIntersection or NetTopologySuite.Operation.Valid.TopologyValidationErrors.NestedHoles)
                    {
                        multiPolygon = GenerateCorrectMultiPolygon(geometry, [isValidOp.ValidationError.Coordinate]);
                        isValidOp = new NetTopologySuite.Operation.Valid.IsValidOp(multiPolygon);
                        if (!isValidOp.IsValid)
                        {
                            return BadRequest(isValidOp.ValidationError.Message);
                        }
                    }
                    else
                    {
                        return BadRequest(isValidOp.ValidationError.Message);
                    }
                }

                var existingRegion = await _context.Regions.SingleOrDefaultAsync(r => r.OsmRelationId == newRegion.OsmRelationId);
                if (existingRegion != null)
                {
                    existingRegion.ParentRegionId = newRegion.ParentRegionId;
                    existingRegion.Name = name;
                    existingRegion.NameNL = nameNL;
                    existingRegion.OriginalName = originalName;
                    existingRegion.Geometry = multiPolygon;
                    _context.Regions.Update(existingRegion);
                }
                else
                {
                    var region = new Region
                    {
                        Name = name,
                        NameNL = nameNL,
                        OriginalName = originalName,
                        OsmRelationId = newRegion.OsmRelationId,
                        Geometry = geometry,
                        ParentRegionId = newRegion.ParentRegionId
                    };
                    _context.Regions.Add(region);
                }
            }
            await _context.SaveChangesAsync();
            return Ok();

        }

        private static NetTopologySuite.Geometries.MultiPolygon GenerateCorrectMultiPolygon(NetTopologySuite.Geometries.MultiPolygon geometry, List<Coordinate> coordinatesToIgnore)
        {
            var geometries = geometry.Geometries.OrderByDescending(g => g.Area).Select(g => (NetTopologySuite.Geometries.Polygon)g).ToList();
            //Find out which geometries are really holes inside another geometry. Then build the MultiPolygon from the outer geometries and the inner geometries.
            var outerGeometries = new Dictionary<NetTopologySuite.Geometries.Polygon, List<NetTopologySuite.Geometries.Polygon>>();
            foreach (var geom in geometries)
            {
                var matched = false;

                foreach (var outer in outerGeometries)
                {
                    foreach (var hole in outer.Key.Holes)
                    {
                        if (hole.Equals(geom.ExteriorRing))
                        {
                            matched = true;
                            break;
                        }
                    }

                    if (outer.Key.Contains(geom) && !matched)
                    {
                        outer.Value.Add(geom);
                        matched = true;
                        break;
                    }
                }
                if (!matched)
                {
                    outerGeometries.Add(geom, new List<NetTopologySuite.Geometries.Polygon>());
                }
            }

            var factory = new GeometryFactory();
            var outers = new List<NetTopologySuite.Geometries.Polygon>();
            foreach (var outerGeom in outerGeometries)
            {

                var existingHoles = outerGeom.Key.Holes;

                existingHoles = existingHoles.Where(h => !coordinatesToIgnore.Any(c => h.Coordinates.Any(c2 => c2.Equals(c)))).ToArray();

                var holes = outerGeom.Value.Where(h => !coordinatesToIgnore.Any(c => h.Coordinates.Any(c2 => c2.Equals(c)))).Select(v => factory.CreateLinearRing(v.Coordinates)).Concat(existingHoles).ToArray();
                var newOuter = factory.CreatePolygon(factory.CreateLinearRing(outerGeom.Key.ExteriorRing.Coordinates), holes);
                outers.Add(newOuter);
            }
            var multiPolygon = factory.CreateMultiPolygon(outers.ToArray());
            return multiPolygon;
        }

        private static Coordinate[] GetCoordinates(Coordinate[] coordinates, List<Coordinate> coordinatesToIgnore)
        {
            if (coordinatesToIgnore == null || !coordinatesToIgnore.Any())
            {
                return coordinates;
            }
            return coordinates.Where(c => !coordinatesToIgnore.Any(c2 => c.Equals(c2))).ToArray();
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RegionDTO>>> GetAll()
        {
            var regions = await _context.Regions.OrderBy(r => r.Name).ProjectTo<RegionIntermediate>(mapper.ConfigurationProvider).ToListAsync();


            var mappedRegions = regions.Where(r => r.ParentRegionId == null).Select(r => new RegionDTO
            {
                Id = r.Id,

                Name = r.Name,
                NameNL = r.NameNL,
                OriginalName = r.OriginalName,
                OsmRelationId = r.OsmRelationId,
                SubRegions = regions.Where(c => c.ParentRegionId == r.Id).Select(c => new RegionDTO
                {
                    Id = c.Id,
                    Name = c.Name,
                    NameNL = c.NameNL,
                    OriginalName = c.OriginalName,
                    OsmRelationId = c.OsmRelationId
                })
            });
            return Ok(mappedRegions);
        }

        [HttpGet("map/{mapGuid}")]
        public async Task<ActionResult<IEnumerable<RegionDTO>>> GetAllForMap(Guid mapGuid)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }

            if (_cache.TryGetValue("regions" + mapGuid, out IEnumerable<RegionDTO> cachedRegions))
            {
                return Ok(cachedRegions);
            }

            var regions = await _context.Regions
                .Where(r => !r.ParentRegionId.HasValue)
                .Where(r => r.Routes.Any(rr => rr.RouteMaps.Any(rm => rm.Map.MapGuid == mapGuid)) || r.Routes.Any(rr => rr.RouteInstances.Any(ri => ri.RouteInstanceMaps.Any(rim => rim.Map.MapGuid == mapGuid))))
                .OrderBy(r => r.Name)
                .ProjectTo<RegionDTO>(mapper.ConfigurationProvider).ToListAsync();

            var mappedRegions = regions.Select(r => new RegionDTO
            {
                Id = r.Id,
                Name = r.Name,
                NameNL = r.NameNL,
                OriginalName = r.OriginalName,
                OsmRelationId = r.OsmRelationId,
                SubRegions = r.SubRegions.Select(c => new RegionDTO
                {
                    Id = c.Id,
                    Name = c.Name,
                    NameNL = c.NameNL,
                    OriginalName = c.OriginalName,
                    OsmRelationId = c.OsmRelationId
                }).OrderBy(s => s.OriginalName)
            });

            _cache.Set("regions" + mapGuid, mappedRegions, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });
            return Ok(mappedRegions);
        }

        [HttpGet("refreshAll")]
        public async Task<IActionResult> RefreshAll()
        {
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
            var regions = _context.Regions.ToList();

            foreach (var region in regions)
            {
                var isValidOp = new NetTopologySuite.Operation.Valid.IsValidOp(region.Geometry);

                if (isValidOp.IsValid)
                {
                    Console.WriteLine("Region " + region.Name + " is valid");
                    continue;
                }


                await CreateNew(new NewRegion { OsmRelationId = region.OsmRelationId, ParentRegionId = region.ParentRegionId });
                Console.WriteLine("Updated region " + region.Name);
                await Task.Delay(1000);
            }
            return Ok();
        }

        [HttpPost("{id}/refreshRoutes")]
        public async Task<IActionResult> RefreshRoutes(int id)
        {
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var routes = await _context.Routes.Where(r => r.Regions.Any(rr => rr.Id == id)).Include(r => r.Regions).ToListAsync();
            foreach (var route in routes)
            {
                await routeRegionsService.AssignRegionsToRouteAsync(route);
            }
            await _context.SaveChangesAsync();
            return Ok(routes.Count);
        }

        [HttpPost("refreshRoutesWithoutRegions")]
        public async Task<IActionResult> RefreshRoutesWithoutRegions()
        {
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
            var routes = await _context.Routes.Where(r => !r.Regions.Any()).Include(r => r.Regions).ToListAsync();
            foreach (var route in routes)
            {
                await routeRegionsService.AssignRegionsToRouteAsync(route);
            }
            await _context.SaveChangesAsync();
            return Ok(routes.Count);
        }

    }
}

