using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OV_DB.Hubs;
using OV_DB.Models;
using OV_DB.Services;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class StationImporterController(OVDBDatabaseContext dbContext, IStationRegionsService stationRegionsService, IHubContext<MapGenerationHub> mapGenerationHubContext, IOverpassService overpassService) : ControllerBase
    {
        private readonly IOverpassService _overpassService = overpassService;
        [HttpPost("region/{regionId}")]
        public async Task<IActionResult> UpdateRegionAsync(int regionId)
        {
            var adminClaim = (User.IsAdmin() ? "true" : "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            // Call the background service to update the region
            await mapGenerationHubContext.Clients.All.SendAsync(MapGenerationHub.RegionStationUpdateMethod, regionId, 0);
            UpdateRegionService.RegionQueue.Enqueue(regionId);

            return Ok();
        }

        [HttpPost("{stationId:long}")]
        public async Task<IActionResult> CreateStation(long stationId)
        {
            var adminClaim = (User.IsAdmin() ? "true" : "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var list = await GetStationAsync(stationId);
            // One quick retry is enough: each attempt already fails over across all mirrors,
            // and this runs inside an HTTP request that shouldn't block for minutes.
            if (list == null)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                list = await GetStationAsync(stationId);
            }

            if (!string.IsNullOrWhiteSpace(list))
            {
                var parsedList = JsonConvert.DeserializeObject<OSMStationList>(list);

                var existingStations = await OsmStationImport.LoadExistingAsync(dbContext, parsedList);
                foreach (var station in parsedList.Elements)
                {
                    await OsmStationImport.UpsertAsync(dbContext, existingStations, station, stationRegionsService);
                }
            }
            await dbContext.SaveChangesAsync();

            return Ok();
        }

        [NonAction]
        public async Task<string> GetStationAsync(long osmId)
        {
            var query = $"[out:json][timeout:30];\r\nnode({osmId});out body;";
            return await _overpassService.QueryAsync(query);
        }
    }
}
