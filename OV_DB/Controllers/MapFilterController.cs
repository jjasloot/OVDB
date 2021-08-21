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


        [HttpGet("countries/{id}")]
        public async Task<ActionResult<List<Country>>> GetCountriesAsync(string id)
        {
            var guid = Guid.Parse(id);
            var map = await DatabaseContext.Maps.SingleOrDefaultAsync(u => u.MapGuid == guid);
            if (map == null)
            {
                return NotFound();
            }
            var list = await DatabaseContext.Countries.Where(c => c.RouteCountries.Any(r => r.Route.RouteMaps.Any(rm => rm.Map.MapId == map.MapId) || r.Route.RouteInstances.Any(ri => ri.RouteInstanceMaps.Any(rim => rim.MapId == map.MapId)))).OrderBy(c => c.Name).ToListAsync();
            return list;
        }


        [HttpGet("years/{id}")]
        public async Task<ActionResult<List<int>>> GetYearsAsync(string id)
        {
            var guid = Guid.Parse(id);
            var map = await DatabaseContext.Maps.SingleOrDefaultAsync(u => u.MapGuid == guid);
            if (map == null)
            {
                return NotFound();
            }
            var years = await DatabaseContext.Routes
             .Where(r => r.RouteTypeId != null)
             .Where(r => r.RouteMaps.Any(rm => rm.MapId == map.MapId) || r.RouteInstances.Any(ri => ri.RouteInstanceMaps.Any(rim => rim.MapId == map.MapId)))
             .SelectMany(r => r.RouteInstances.Select(ri => ri.Date.Year))
             .Distinct()
             .ToListAsync();
            return Ok(years);
        }

        [HttpGet("types/{id}")]
        public async Task<ActionResult<List<RouteType>>> GetTypesAsync(string id)
        {
            var guid = Guid.Parse(id);
            var map = await DatabaseContext.Maps.SingleOrDefaultAsync(u => u.MapGuid == guid);
            if (map == null)
            {
                return NotFound();
            }
            var list = await DatabaseContext.RouteTypes
                .Where(rt => rt.Routes.Any(r => r.RouteMaps.Any(rm => rm.MapId == map.MapId) || r.RouteInstances.Any(ri => ri.RouteInstanceMaps.Any(rim => rim.MapId == map.MapId))))
                .OrderBy(r => r.OrderNr).ToListAsync();
            return list;
        }
    }
}