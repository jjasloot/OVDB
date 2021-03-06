﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OV_DB.Helpers;
using OV_DB.Models;
using OVDB_database.Database;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly OVDBDatabaseContext _dbContext;

        public AdminController(OVDBDatabaseContext dbContext, IConfiguration configuration)
        {
            _configuration = configuration;
            _dbContext = dbContext;
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
                RouteCount = u.Maps.Sum(m => m.RouteMaps.Count)
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
                } catch(Exception ex)
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
    }
}