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
        private readonly OVDBDatabaseContext _dbContext;
        private readonly ILogger<AdminController> _logger;

        public AdminController(OVDBDatabaseContext dbContext, ILogger<AdminController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
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

    }
}