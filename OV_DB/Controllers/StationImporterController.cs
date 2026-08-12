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
    public class StationImporterController(OVDBDatabaseContext dbContext, IStationRegionsService stationRegionsService, IHubContext<MapGenerationHub> mapGenerationHubContext, IOverpassService overpassService) : ControllerBase
    {
        private readonly IOverpassService _overpassService = overpassService;
        [HttpPost("region/{regionId}")]
        public async Task<IActionResult> UpdateRegionAsync(int regionId)
        {
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
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
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
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

                var osmIds = parsedList.Elements.Select(e => e.Id).ToList();
                var existingStations = await dbContext.Stations
                    .Where(s => osmIds.Contains(s.OsmId))
                    .ToDictionaryAsync(s => s.OsmId);

                foreach (var station in parsedList.Elements)
                {
                    if (station.Tags.ContainsKey("name") && !string.IsNullOrWhiteSpace(station.Tags["name"]) && !(station.Lat == 0 && station.Lon == 0))
                    {
                        if (!existingStations.TryGetValue(station.Id, out var stationToUpdate))
                        {
                            stationToUpdate = new Station { OsmId = station.Id };
                            dbContext.Add(stationToUpdate);
                            existingStations[station.Id] = stationToUpdate;
                        }
                        stationToUpdate.Lattitude = station.Lat;
                        stationToUpdate.Longitude = station.Lon;
                        if (station.Tags.ContainsKey("name"))
                            stationToUpdate.Name = station.Tags["name"];
                        if (station.Tags.ContainsKey("ele") && double.TryParse(station.Tags["ele"], NumberStyles.Float, CultureInfo.InvariantCulture, out var elevation))
                            stationToUpdate.Elevation = elevation;
                        if (station.Tags.ContainsKey("network"))
                            stationToUpdate.Network = station.Tags["network"];
                        if (station.Tags.ContainsKey("operator"))
                            stationToUpdate.Operator = station.Tags["operator"];
                        if (station.Tags.ContainsKey("usage") && station.Tags["usage"] == "tourism")
                        {
                            stationToUpdate.Special = true;
                        }
                        await stationRegionsService.AssignRegionsToStationCacheRegionsAsync(stationToUpdate);
                    }
                }
            }
            await dbContext.SaveChangesAsync();

            return Ok();
        }

        [NonAction]
        public async Task<string> GetStationListAsync(string osmId)
        {
            var query = $"[out:json][timeout:180];area({osmId})->.searchArea;(node[\"railway\"=\"station\"][!\"subway\"][!\"funicular\"][!\"tram\"][\"station\"!=\"monorail\"][\"station\"!=\"subway\"][\"station\"!=\"tram\"](area.searchArea);node[\"railway\"=\"station\"][\"train\"=\"yes\"](area.searchArea);node[\"railway\"=\"halt\"][!\"subway\"][!\"funicular\"][!\"tram\"][\"station\"!=\"monorail\"][\"station\"!=\"subway\"][\"station\"!=\"tram\"](area.searchArea);node[\"railway\"=\"halt\"][\"train\"=\"yes\"](area.searchArea););out body;";
            return await _overpassService.QueryAsync(query);
        }

        [NonAction]
        public async Task<string> GetStationAsync(long osmId)
        {
            var query = $"[out:json][timeout:30];\r\nnode({osmId});out body;";
            return await _overpassService.QueryAsync(query);
        }

        [NonAction]
        public async Task<string> GetStationWayList(string osmId)
        {
            var query = $"[out:json][timeout:180];area({osmId})->.searchArea;(way[\"railway\"=\"station\"][!\"subway\"][!\"funicular\"][!\"tram\"][\"station\"!=\"monorail\"][\"station\"!=\"subway\"][\"station\"!=\"tram\"](area.searchArea);node[\"railway\"=\"station\"][\"train\"=\"yes\"](area.searchArea);node[\"railway\"=\"halt\"][!\"subway\"][!\"funicular\"][!\"tram\"][\"station\"!=\"monorail\"][\"station\"!=\"subway\"][\"station\"!=\"tram\"](area.searchArea);way[\"railway\"=\"halt\"][\"train\"=\"yes\"](area.searchArea););out center;";
            return await _overpassService.QueryAsync(query);
        }
    }
}
