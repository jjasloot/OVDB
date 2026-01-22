using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
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
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            return await _context.Maps.AsNoTracking().Where(m => m.UserId == userIdClaim).OrderBy(m => m.OrderNr).ToListAsync();
        }

        // GET: api/Maps/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Map>> GetMap(int id)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
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
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var dbMap = await _context.Maps.Where(m => m.UserId == userIdClaim).SingleOrDefaultAsync(m => m.MapId == map.MapId);

            dbMap.SharingLinkName = map.SharingLinkName;
            dbMap.Name = map.Name;
            dbMap.NameNL = map.NameNL;
            dbMap.OrderNr = map.OrderNr;
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
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
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
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
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
            _context.RoutesMaps.RemoveRange(routeMaps);
            _context.Maps.Remove(map);
            await _context.SaveChangesAsync();

            return map;
        }

        [HttpPost("order")]
        public async Task<ActionResult> UpdateMapsOrdering([FromBody] List<int> mapOrdering)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
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
