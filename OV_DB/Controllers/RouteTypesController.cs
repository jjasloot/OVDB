using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OVDB_database.Database;
using OVDB_database.Models;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RouteTypesController : ControllerBase
    {
        private readonly OVDBDatabaseContext _context;

        public RouteTypesController(OVDBDatabaseContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RouteType>>> GetRouteTypes()
        {
            var userIdClaim = User.GetUserId();
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            return await _context.RouteTypes.OrderBy(r => r.OrderNr).Where(r => r.UserId == userIdClaim).ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RouteType>> GetRouteType(int id)
        {
            var userIdClaim = User.GetUserId();
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var routeType = await _context.RouteTypes.Where(r => r.UserId == userIdClaim).SingleOrDefaultAsync(r => r.TypeId == id);

            if (routeType == null)
            {
                return NotFound();
            }

            return routeType;
        }
        [HttpPost]
        public async Task<ActionResult> AddRouteType([FromBody] RouteType routeType)
        {
            var userIdClaim = User.GetUserId();
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            routeType.UserId = userIdClaim;
            _context.RouteTypes.Add(routeType);
            await _context.SaveChangesAsync();
            return Ok(routeType.TypeId);
        }

        /// <summary>
        /// The update body. <see cref="MatchesRailNetworks"/> is nullable so a client that predates
        /// the flag (and omits it) leaves the stored value alone instead of clearing it.
        /// </summary>
        public sealed class RouteTypeUpdate
        {
            public int TypeId { get; set; }
            [System.ComponentModel.DataAnnotations.Required]
            public string Name { get; set; }
            public string NameNL { get; set; }
            [System.ComponentModel.DataAnnotations.Required]
            public string Colour { get; set; }
            public bool IsTrain { get; set; }
            public string TrainlogType { get; set; }
            public bool? MatchesRailNetworks { get; set; }
        }

        [HttpPut]
        public async Task<ActionResult> UpdateRouteType([FromBody] RouteTypeUpdate routeType)
        {
            var userIdClaim = User.GetUserId();
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var dbRouteType = await _context.RouteTypes.Where(c => c.UserId == userIdClaim).SingleOrDefaultAsync(c => c.TypeId == routeType.TypeId);
            if (dbRouteType == null)
            {
                return NotFound();
            }
            dbRouteType.Name = routeType.Name;
            dbRouteType.NameNL = routeType.NameNL;
            dbRouteType.Colour = routeType.Colour;
            dbRouteType.IsTrain = routeType.IsTrain;
            dbRouteType.TrainlogType = routeType.TrainlogType;
            var railFlagChanged = routeType.MatchesRailNetworks is { } matches && matches != dbRouteType.MatchesRailNetworks;
            if (railFlagChanged)
                dbRouteType.MatchesRailNetworks = routeType.MatchesRailNetworks.Value;

            await _context.SaveChangesAsync();
            if (railFlagChanged)
            {
                // The flag decides whether this type's routes cover rail networks at all.
                var routeIds = await _context.Routes.Where(r => r.RouteTypeId == dbRouteType.TypeId && r.LineString != null).Select(r => r.RouteId).ToListAsync();
                foreach (var routeId in routeIds)
                    OV_DB.Services.Network.NetworkMatchQueueService.Queue.Enqueue(new OV_DB.Services.Network.RouteChanged(routeId));
            }
            return Ok(routeType.TypeId);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteRouteType(int id)
        {
            var userIdClaim = User.GetUserId();
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var routeType = await _context.RouteTypes.SingleOrDefaultAsync(c => c.TypeId == id);
            if (routeType == null)
            {
                return NotFound();
            }
            if (routeType.UserId != userIdClaim)
            {
                return Forbid();
            }
            var routes = await _context.Routes.Where(r => r.RouteTypeId == id).ToListAsync();
            routes.ForEach(r => r.RouteTypeId = null);
            _context.RouteTypes.Remove(routeType);
            _context.Routes.UpdateRange(routes);
            await _context.SaveChangesAsync();
            // Without a type these routes no longer match rail networks; drop their coverage.
            foreach (var route in routes)
                OV_DB.Services.Network.NetworkMatchQueueService.Queue.Enqueue(new OV_DB.Services.Network.RouteChanged(route.RouteId));
            return Ok();
        }

        [HttpPost("order")]
        public async Task<ActionResult> UpdateRouteTypeOrdering([FromBody] List<int> routeTypeOrder)
        {
            var userIdClaim = User.GetUserId();
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var routeTypes = await _context.RouteTypes.Where(rt => rt.UserId == userIdClaim).ToListAsync();

            routeTypes.ForEach(r => r.OrderNr = routeTypeOrder.FindIndex(i => r.TypeId == i));
            _context.RouteTypes.UpdateRange(routeTypes);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}
