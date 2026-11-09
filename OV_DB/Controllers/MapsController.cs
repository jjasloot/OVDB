using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MapsController : ControllerBase
    {
        private readonly OVDBDatabaseContext _context;

        public MapsController(OVDBDatabaseContext context)
        {
            _context = context;
        }

        // GET: api/Maps
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Map>>> GetMaps()
        {
            var userIdClaim = User.GetUserId();
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            return await _context.Maps.AsNoTracking()
                .Where(m => m.UserId == userIdClaim)
                .OrderBy(m => m.Completed)
                .ThenBy(m => m.OrderNr)
                .ToListAsync();
        }

        // GET: api/Maps/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Map>> GetMap(int id)
        {
            var userIdClaim = User.GetUserId();
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var map = await _context.Maps.AsNoTracking().Where(m => m.UserId == userIdClaim).SingleOrDefaultAsync(m => m.MapId == id);

            if (map == null)
            {
                return NotFound();
            }

            return map;
        }

        // PUT: api/Maps/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
        [HttpPut]
        public async Task<IActionResult> PutMap(Map map)
        {
            var userIdClaim = User.GetUserId();
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var dbMap = await _context.Maps.Where(m => m.UserId == userIdClaim).SingleOrDefaultAsync(m => m.MapId == map.MapId);
            if (dbMap == null)
            {
                return NotFound();
            }

            // OrderNr is not copied: Map is opt-in for JSON and OrderNr has no [JsonProperty], so it
            // always arrives as 0 and every save moved the map to the top. Order has its own endpoint.
            dbMap.SharingLinkName = map.SharingLinkName;
            dbMap.Name = map.Name;
            dbMap.NameNL = map.NameNL;
            dbMap.Default = map.Default;
            dbMap.ShowRouteInfo = map.ShowRouteInfo;
            dbMap.ShowRouteOutline = map.ShowRouteOutline;
            dbMap.Completed = map.Completed;
            if (map.Default == true)
            {
                var maps = await _context.Maps.Where(m => m.UserId == userIdClaim && m.MapId != map.MapId).ToListAsync();
                maps.ForEach(m => m.Default = false);
                _context.Maps.UpdateRange(maps);
            }
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MapExists(map.MapId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Maps
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
        [HttpPost]
        public async Task<ActionResult<Map>> PostMap(Map map)
        {
            var userIdClaim = User.GetUserId();
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            map.UserId = userIdClaim;
            map.MapGuid = Guid.NewGuid();
            if (map.Default == true)
            {
                var maps = await _context.Maps.Where(m => m.UserId == userIdClaim).ToListAsync();
                maps.ForEach(m => m.Default = false);
                _context.Maps.UpdateRange(maps);
            }
            _context.Maps.Add(map);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetMap", new { id = map.MapId }, map);
        }

        // DELETE: api/Maps/5
        [HttpDelete("{id}")]
        public async Task<ActionResult<Map>> DeleteMap(int id)
        {
            var userIdClaim = User.GetUserId();
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var map = await _context.Maps.Where(m => m.UserId == userIdClaim).SingleOrDefaultAsync(m => m.MapId == id);
            if (map == null)
            {
                return NotFound();
            }
            var routeMaps = await _context.RoutesMaps.Where(rm => rm.MapId == map.MapId).ToListAsync();

            // A route on none of the user's maps can never be seen, edited or deleted again, so
            // routes that only live on this map move to the default map (or another one).
            var routeIds = routeMaps.Select(rm => rm.RouteId).ToList();
            var orphanedRouteIds = await _context.Routes
                .Where(r => routeIds.Contains(r.RouteId) && !r.RouteMaps.Any(rm => rm.MapId != map.MapId && rm.Map.UserId == userIdClaim))
                .Select(r => r.RouteId)
                .ToListAsync();
            if (orphanedRouteIds.Count > 0)
            {
                var target = await _context.Maps
                    .Where(m => m.UserId == userIdClaim && m.MapId != map.MapId)
                    .OrderByDescending(m => m.Default)
                    .ThenBy(m => m.OrderNr)
                    .FirstOrDefaultAsync();
                if (target == null)
                {
                    return Conflict("This is your only map and it still has routes");
                }
                _context.RoutesMaps.AddRange(orphanedRouteIds.Select(id => new RouteMap { RouteId = id, MapId = target.MapId }));
            }
            _context.RoutesMaps.RemoveRange(routeMaps);
            _context.Maps.Remove(map);
            await _context.SaveChangesAsync();

            return map;
        }

        [HttpPost("order")]
        public async Task<ActionResult> UpdateMapsOrdering([FromBody] List<int> mapOrdering)
        {
            var userIdClaim = User.GetUserId();
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var maps = await _context.Maps.Where(rt => rt.UserId == userIdClaim).ToListAsync();

            maps.ForEach(r => r.OrderNr = mapOrdering.FindIndex(i => r.MapId == i));
            _context.Maps.UpdateRange(maps);
            await _context.SaveChangesAsync();
            return Ok();
        }

        private bool MapExists(int id)
        {
            return _context.Maps.Any(e => e.MapId == id);
        }
    }
}
