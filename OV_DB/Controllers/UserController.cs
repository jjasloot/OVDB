using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        public OVDBDatabaseContext DatabaseContext { get; }
        public UserController(OVDBDatabaseContext databaseContext)
        {
            DatabaseContext = databaseContext;
        }

        [HttpGet("maps")]
        public async Task<ActionResult<List<Map>>> GetMapsAsync()
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var maps = await DatabaseContext.Maps.Where(m => m.UserId == userIdClaim).ToListAsync();
            return Ok(maps);
        }

        [HttpGet("link/{name}")]
        [AllowAnonymous]
        public async Task<ActionResult<string>> GetGuidFromNameAsync(string name)
        {
            var map = await DatabaseContext.Maps.Where(m => m.SharingLinkName == name).SingleOrDefaultAsync();
            if (map == null)
            {
                return NotFound();
            }
            return Ok(map.MapGuid);
        } 

        [HttpGet("station-link/{name}")]
        [AllowAnonymous]
        public async Task<ActionResult<string>> GetGuidFromNameForStationsAsync(string name)
        {
            var map = await DatabaseContext.StationMaps.Where(m => m.SharingLinkName == name).SingleOrDefaultAsync();
            if (map == null)
            {
                return NotFound();
            }
            return Ok(map.MapGuid);
        }

        //[HttpGet("overwriteGUIDs")]
        //[AllowAnonymous]
        //public async Task<ActionResult> ResetAllGuids()
        //{
        //    var maps = await DatabaseContext.Maps.ToListAsync();
        //    Guid.NewGuid();
        //    maps.ForEach(m => m.MapGuid = Guid.NewGuid());
        //    DatabaseContext.UpdateRange(maps);
        //    await DatabaseContext.SaveChangesAsync();
        //    return Ok();
        //}
    }
}