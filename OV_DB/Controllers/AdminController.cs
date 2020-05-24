using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

        [HttpGet("useragent")]
        public ActionResult GetUserAgent()
        {
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
            var userAgent = _configuration.GetValue<string>("UserAgent", "OVDB");
            return Ok(userAgent);
        }
    }
}