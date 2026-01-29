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
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            return await _context.RouteTypes.OrderBy(r => r.OrderNr).Where(r => r.UserId == userIdClaim).ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RouteType>> GetRouteType(int id)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
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
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            routeType.UserId = userIdClaim;
            _context.RouteTypes.Add(routeType);
            await _context.SaveChangesAsync();
            return Ok(routeType.TypeId);
        }

        [HttpPut]
        public async Task<ActionResult> UpdateRouteType([FromBody] RouteType routeType)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
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

            await _context.SaveChangesAsync();
            return Ok(routeType.TypeId);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteRouteType(int id)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
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
            return Ok();
        }

        [HttpPost("order")]
        public async Task<ActionResult> UpdateRouteTypeOrdering([FromBody] List<int> routeTypeOrder)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
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
