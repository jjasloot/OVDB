using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NetTopologySuite.Geometries;
using Newtonsoft.Json;
using OV_DB.Helpers;
using OV_DB.Models;
using OV_DB.Services;
using OVDB_database.Database;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly OVDBDatabaseContext _dbContext;
        private readonly IHttpClientFactory _httpClientFactory;

        public AdminController(OVDBDatabaseContext dbContext, IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _dbContext = dbContext;
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost("AddMissingGuidsForRoute")]
        public async Task<ActionResult> AddMissingGuidsForRoutes()
        {
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var routesMissingGuids = await _dbContext.Routes.Where(r => r.Share == Guid.Empty).ToListAsync();

            routesMissingGuids.ForEach(r => r.Share = Guid.NewGuid());

            await _dbContext.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("users")]
        public async Task<ActionResult> GetAdministratorUsers()
        {
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var list = await _dbContext.Users.Select(u => new AdminUser
            {
                Id = u.Id,
                Email = u.Email,
                LastLogin = u.LastLogin,
                IsAdmin = u.IsAdmin,
                RouteCount = u.Maps.Sum(m => m.RouteMaps.Count),
                
                // Calculate route instances statistics for user's routes
                RouteInstancesCount = u.Maps
                    .SelectMany(m => m.RouteMaps)
                    .Select(rm => rm.Route)
                    .SelectMany(r => r.RouteInstances)
                    .Count(),
                    
                RouteInstancesWithTimeCount = u.Maps
                    .SelectMany(m => m.RouteMaps)
                    .Select(rm => rm.Route)
                    .SelectMany(r => r.RouteInstances)
                    .Count(ri => ri.StartTime.HasValue && ri.EndTime.HasValue),
                    
                RouteInstancesWithTrawellingIdCount = u.Maps
                    .SelectMany(m => m.RouteMaps)
                    .Select(rm => rm.Route)
                    .SelectMany(r => r.RouteInstances)
                    .Count(ri => ri.TrawellingStatusId.HasValue),
                    
                LastRouteInstanceDate = u.Maps
                    .SelectMany(m => m.RouteMaps)
                    .Select(rm => rm.Route)
                    .SelectMany(r => r.RouteInstances)
                    .OrderByDescending(ri => ri.Date)
                    .Select(ri => (DateTime?)ri.Date)
                    .FirstOrDefault()
            }).ToListAsync();

            return Ok(list);
        }

        [HttpGet("maps")]
        public async Task<ActionResult> GetAdministratorMaps()
        {
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var list = await _dbContext.Maps.Select(m => new AdminMap
            {
                Id = m.MapId,
                Guid = m.MapGuid,
                MapName = m.Name,
                RouteCount = m.RouteMaps.Count,
                ShareLink = m.SharingLinkName,
                UserEmail = m.User.Email
            }).ToListAsync();

            return Ok(list);
        }
        [HttpGet("distance/{id:int}")]
        public async Task<ActionResult> CalculateDistanceById(int id)
        {
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var route = await _dbContext.Routes.FindAsync(id);

            if (route != null)
            {
                DistanceCalculationHelper.ComputeDistance(route);
            }
            await _dbContext.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("distance/missing")]
        public async Task<ActionResult> CalculateDistanceForAllMissing()
        {
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var routes = await _dbContext.Routes.Where(r => r.CalculatedDistance == 0).ToListAsync();

            routes.ForEach(route =>
            {
                DistanceCalculationHelper.ComputeDistance(route);
            });
            await _dbContext.SaveChangesAsync();
            return Ok();
        }
        [HttpGet("distance/all")]
        public async Task<ActionResult> CalculateDistanceForAll()
        {
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var routes = await _dbContext.Routes.ToListAsync();

            routes.ForEach(route =>
            {
                try
                {
                    DistanceCalculationHelper.ComputeDistance(route);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            });
            await _dbContext.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("convertToInstances")]
        public async Task<ActionResult> ConvertToInstances()
        {
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var routes = await _dbContext.Routes.Include(r => r.RouteInstances).ToListAsync();

            routes.ForEach(r =>
            {
                if (r.FirstDateTime.HasValue)
                {
                    if (!r.RouteInstances.Any(ri => ri.Date == r.FirstDateTime))
                    {
                        r.RouteInstances.Add(new OVDB_database.Models.RouteInstance { Date = r.FirstDateTime.Value });
                    }
                }
            });

            await _dbContext.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("addRegions")]
        public async Task<ActionResult> AddRegionsToAllRoutes([FromServices] IRouteRegionsService routeRegionsService)
        {
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
            _dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(3));
            var batchSize = 50;
            var count = 0;
            var routes = new List<OVDB_database.Models.Route>();
            do
            {
                routes = await _dbContext.Routes.OrderBy(r => r.Name).Where(r => r.LineString != null).Include(r => r.Regions).Skip(count).Take(batchSize).ToListAsync();

                foreach (var route in routes)
                {
                    var regionsBefore = string.Join(", ", route.Regions.Select(r => r.Name));
                    await routeRegionsService.AssignRegionsToRouteAsync(route);
                    Console.WriteLine($"Route {route.Name} from {regionsBefore} the following Regions: " + string.Join(", ", route.Regions.Select(r => r.Name)));
                }
                await _dbContext.SaveChangesAsync();
                count += batchSize;
                Console.WriteLine($"Added regions to {count} routes");
            } while (routes.Count > 0);

            return Ok();
        }

        [HttpGet("fixOriginalnames")]
        public async Task<ActionResult> FixOriginalNames()
        {
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
            _dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(3));
            var regions = await _dbContext.Regions.Where(r => string.IsNullOrWhiteSpace(r.OriginalName)).ToListAsync();
            foreach (var region in regions)
            {
                var tags = await GetTagsAsync(region.OsmRelationId);
                if (tags != null && tags.ContainsKey("name"))
                {
                    region.OriginalName = tags["name"];
                }
                await _dbContext.SaveChangesAsync();
                Console.WriteLine($"Region {region.Name} updated with original name {region.OriginalName}");
                await Task.Delay(250);
            }

            return Ok();
        }

        [HttpGet("assignRegionsToStations")]
        [AllowAnonymous]
        public async Task<ActionResult> AssignRegionsToStations([FromServices] IStationRegionsService stationRegionsService, [FromQuery] int? regionId)
        {
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
            _dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(3));
            var batchSize = 50;
            var count = 0;
            var query = _dbContext.Stations.OrderBy(s => s.Name).Where(s => s.Lattitude != 0 && s.Longitude != 0).Include(s => s.Regions).AsQueryable();
            if (regionId.HasValue)
            {
                query = query.Where(s => s.Regions.Any(r => r.Id == regionId.Value));
            }
            var stations = new List<OVDB_database.Models.Station>();
            do
            {
                stations = await query.Skip(count).Take(batchSize).ToListAsync();

                foreach (var station in stations)
                {
                    var regionsBefore = string.Join(", ", station.Regions.Select(r => r.Name));
                    await stationRegionsService.AssignRegionsToStationCacheRegionsAsync(station);
                    Console.WriteLine($"Station {station.Name} from {regionsBefore} the following Regions: " + string.Join(", ", station.Regions.Select(r => r.Name)));
                }
                await _dbContext.SaveChangesAsync();
                count += batchSize;
                Console.WriteLine($"Added regions to {count} stations");
            } while (stations.Count > 0);

            return Ok();
        }

        private async Task<Dictionary<string, string>> GetTagsAsync(long id)
        {
            var query = $"[out:json]";
            query += $";relation({id});";
            query += "out tags;";
            string text = null;
            var httpClient = _httpClientFactory.CreateClient("OSM");

            var response = await httpClient.PostAsync("https://overpass-api.de/api/interpreter", new StringContent(query));
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                return null;
            }
            text = await response.Content.ReadAsStringAsync();
            
            var parsed = JsonConvert.DeserializeObject<OSM>(text.ToString());
            return parsed.Elements.Single().Tags;
        }
    }
}