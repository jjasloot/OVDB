using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
    [Authorize]
    public class AdminController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly OVDBDatabaseContext _dbContext;
        private readonly IOverpassService _overpassService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(OVDBDatabaseContext dbContext, IConfiguration configuration, IOverpassService overpassService, ILogger<AdminController> logger)
        {
            _configuration = configuration;
            _dbContext = dbContext;
            _overpassService = overpassService;
            _logger = logger;
        }

        [HttpPost("AddMissingGuidsForRoute")]
        public async Task<ActionResult> AddMissingGuidsForRoutes()
        {
            if (!User.IsAdmin())
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
            if (!User.IsAdmin())
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
            if (!User.IsAdmin())
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
            if (!User.IsAdmin())
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
            if (!User.IsAdmin())
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
        public async Task<ActionResult> CalculateDistanceForAll(CancellationToken cancellationToken)
        {
            if (!User.IsAdmin())
            {
                return Forbid();
            }

            var routes = await _dbContext.Routes.ToListAsync(cancellationToken);

            routes.ForEach(route =>
            {
                try
                {
                    DistanceCalculationHelper.ComputeDistance(route);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to compute distance for route {RouteId}", route.RouteId);
                }
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok();
        }

        [HttpGet("convertToInstances")]
        public async Task<ActionResult> ConvertToInstances()
        {
            if (!User.IsAdmin())
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
        public async Task<ActionResult> AddRegionsToAllRoutes([FromServices] IRouteRegionsService routeRegionsService, CancellationToken cancellationToken)
        {
            if (!User.IsAdmin())
            {
                return Forbid();
            }
            _dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(3));
            var batchSize = 50;
            var count = 0;
            var routes = new List<OVDB_database.Models.Route>();
            do
            {
                routes = await _dbContext.Routes.OrderBy(r => r.Name).Where(r => r.LineString != null).Include(r => r.Regions).Skip(count).Take(batchSize).ToListAsync(cancellationToken);

                foreach (var route in routes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var regionsBefore = string.Join(", ", route.Regions.Select(r => r.Name));
                    await routeRegionsService.AssignRegionsToRouteAsync(route);
                    _logger.LogDebug("Route {RouteName} regions {Before} => {After}", route.Name, regionsBefore, string.Join(", ", route.Regions.Select(r => r.Name)));
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
                count += batchSize;
                _logger.LogInformation("Added regions to {Count} routes", count);
            } while (routes.Count > 0);

            return Ok();
        }

        [HttpGet("fixOriginalnames")]
        public async Task<ActionResult> FixOriginalNames(CancellationToken cancellationToken)
        {
            if (!User.IsAdmin())
            {
                return Forbid();
            }
            _dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(3));
            var regions = await _dbContext.Regions.Where(r => string.IsNullOrWhiteSpace(r.OriginalName)).ToListAsync(cancellationToken);
            foreach (var region in regions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var tags = await GetTagsAsync(region.OsmRelationId);
                if (tags != null && tags.ContainsKey("name"))
                {
                    region.OriginalName = tags["name"];
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Region {RegionName} updated with original name {OriginalName}", region.Name, region.OriginalName);
                await Task.Delay(250, cancellationToken);
            }

            return Ok();
        }

        [HttpGet("assignRegionsToStations")]
        public async Task<ActionResult> AssignRegionsToStations([FromServices] IStationRegionsService stationRegionsService, [FromQuery] int? regionId, CancellationToken cancellationToken)
        {
            if (!User.IsAdmin())
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
                stations = await query.Skip(count).Take(batchSize).ToListAsync(cancellationToken);

                foreach (var station in stations)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var regionsBefore = string.Join(", ", station.Regions.Select(r => r.Name));
                    await stationRegionsService.AssignRegionsToStationCacheRegionsAsync(station);
                    _logger.LogDebug("Station {StationName} regions {Before} => {After}", station.Name, regionsBefore, string.Join(", ", station.Regions.Select(r => r.Name)));
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
                count += batchSize;
                _logger.LogInformation("Added regions to {Count} stations", count);
            } while (stations.Count > 0);

            return Ok();
        }

        private async Task<Dictionary<string, string>> GetTagsAsync(long id)
        {
            var query = $"[out:json][timeout:30]";
            query += $";relation({id});";
            query += "out tags;";
            var text = await _overpassService.QueryAsync(query);
            if (text == null)
            {
                return null;
            }

            var parsed = JsonConvert.DeserializeObject<OSM>(text);
            return parsed.Elements.Single().Tags;
        }
    }
}