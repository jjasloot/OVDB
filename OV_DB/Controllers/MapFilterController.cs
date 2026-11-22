using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class MapFilterController : ControllerBase
    {
        public OVDBDatabaseContext DatabaseContext { get; }
        public MapFilterController(OVDBDatabaseContext databaseContext)
        {
            DatabaseContext = databaseContext;
        }

        [HttpGet("years/{id}")]
        public async Task<ActionResult<List<int>>> GetYearsAsync(string id)
        {
            var guid = Guid.Parse(id);
            var map = await DatabaseContext.Maps.AsNoTracking().SingleOrDefaultAsync(u => u.MapGuid == guid);
            if (map == null || !User.CanView(map))
            {
                return NotFound();
            }
            var years = await DatabaseContext.RouteInstances
             .Where(ri => ri.Route.RouteTypeId != null)
             .Where(ri => ri.Route.RouteMaps.Any(rm => rm.MapId == map.MapId) || ri.RouteInstanceMaps.Any(rim => rim.MapId == map.MapId))
             .Select(ri => ri.Date.Year)
             .Distinct()
             .ToListAsync();
            return Ok(years);
        }

        [HttpGet("types/{id}")]
        public async Task<ActionResult<List<RouteType>>> GetTypesAsync(string id)
        {
            var guid = Guid.Parse(id);
            var map = await DatabaseContext.Maps.AsNoTracking().SingleOrDefaultAsync(u => u.MapGuid == guid);
            if (map == null || !User.CanView(map))
            {
                return NotFound();
            }
            var list = await DatabaseContext.RouteTypes
                .Where(rt => DatabaseContext.Routes
                    .Where(r => r.RouteTypeId == rt.TypeId)
                    .Any(r => r.RouteMaps.Any(rm => rm.MapId == map.MapId) || r.RouteInstances.Any(ri => ri.RouteInstanceMaps.Any(rim => rim.MapId == map.MapId))))
                .OrderBy(r => r.OrderNr).ToListAsync();
            return list;
        }

        /// <summary>
        /// The trip properties used on a map, each with its most common values, for the map filter's
        /// property conditions. Capped per key: vehicle numbers run into the thousands.
        /// </summary>
        [HttpGet("properties/{id}")]
        public async Task<ActionResult> GetPropertiesAsync(string id)
        {
            var guid = Guid.Parse(id);
            var map = await DatabaseContext.Maps.AsNoTracking().SingleOrDefaultAsync(u => u.MapGuid == guid);
            if (map == null || !User.CanView(map))
            {
                return NotFound();
            }
            var rows = await DatabaseContext.RouteInstanceProperties
                .AsNoTracking()
                .Where(p => p.RouteInstance.Route.RouteMaps.Any(rm => rm.MapId == map.MapId) || p.RouteInstance.RouteInstanceMaps.Any(rim => rim.MapId == map.MapId))
                .GroupBy(p => new { p.Key, p.Value, IsBool = p.Bool != null })
                .Select(g => new { g.Key.Key, g.Key.Value, g.Key.IsBool, Count = g.Count() })
                .ToListAsync();
            var result = rows
                .Where(r => !string.IsNullOrWhiteSpace(r.Key))
                .GroupBy(r => r.Key)
                .Select(g => new
                {
                    key = g.Key,
                    isBool = g.Any(r => r.IsBool) && g.All(r => r.IsBool || string.IsNullOrEmpty(r.Value)),
                    count = g.Sum(r => r.Count),
                    values = g.Where(r => !string.IsNullOrWhiteSpace(r.Value))
                        .OrderByDescending(r => r.Count).Take(300).Select(r => r.Value).ToList(),
                })
                .OrderByDescending(k => k.count)
                .ToList();
            return Ok(result);
        }

        /// <summary>The operators of the routes on a map, for the map filter's operator list.</summary>
        [HttpGet("operators/{id}")]
        public async Task<ActionResult> GetOperatorsAsync(string id)
        {
            var guid = Guid.Parse(id);
            var map = await DatabaseContext.Maps.AsNoTracking().SingleOrDefaultAsync(u => u.MapGuid == guid);
            if (map == null || !User.CanView(map))
            {
                return NotFound();
            }
            var operators = await DatabaseContext.Operators
                .AsNoTracking()
                .Where(o => o.Routes.Any(r => r.RouteMaps.Any(rm => rm.MapId == map.MapId) || r.RouteInstances.Any(ri => ri.RouteInstanceMaps.Any(rim => rim.MapId == map.MapId))))
                .Select(o => new { o.Id, o.Names })
                .ToListAsync();
            // Names is a value-converted list, so the primary (first) name is picked and sorted on here.
            return Ok(operators
                .Select(o => new { id = o.Id, name = o.Names.FirstOrDefault() ?? "" })
                .OrderBy(o => o.name, StringComparer.CurrentCultureIgnoreCase));
        }
    }
}