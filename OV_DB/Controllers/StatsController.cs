using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OVDB_database.Database;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StatsController : ControllerBase
    {
        private readonly OVDBDatabaseContext _context;

        public StatsController(OVDBDatabaseContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult> GetStats([FromQuery] int? year)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }

            var query = _context.Routes
                .Where(r => r.RouteMaps.Any(rm => rm.Map.UserId == userIdClaim));

            if (year.HasValue)
            {
                query = query.Where(r => r.RouteInstances.Any(ri => ri.Date.Year == year));
            }

            var x = await query.Select(ro => new
            {
                ro.RouteType.Name,
                ro.RouteType.NameNL,
                Distance = ((ro.OverrideDistance.HasValue && ro.OverrideDistance > 0) ? ro.OverrideDistance : ro.CalculatedDistance) * ro.RouteInstances.Count
            }).ToListAsync();

            var x2 = x.GroupBy(x => x.Name).Select(x => new { Name = x.Key, NameNl = x.First().NameNL, Distance = x.Sum(x => x.Distance) });
            return Ok(x2);
        }
    }
}
