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
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StationImporterController(OVDBDatabaseContext dbContext, IStationRegionsService stationRegionsService, IHubContext<MapGenerationHub> mapGenerationHubContext) : ControllerBase
    {
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

        [HttpPost("{stationId}")]
        public async Task<IActionResult> CreateStation(string stationId)
        {
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var list = await GetStationAsync(stationId);
            var tryCount = 1;
            while (list == null && tryCount < 6)
            {
                tryCount++;
                await Task.Delay((int)(10000 * (Math.Pow(2, tryCount))));
                list = await GetStationAsync(stationId);
            }

            if (!string.IsNullOrWhiteSpace(list))
            {
                var parsedList = JsonConvert.DeserializeObject<OSMStationList>(list);

                foreach (var station in parsedList.Elements)
                {
                    if (station.Tags.ContainsKey("name") && !string.IsNullOrWhiteSpace(station.Tags["name"]) && !(station.Lat == 0 && station.Lon == 0))
                    {
                        Station stationToUpdate = null;

                        if (await dbContext.Stations.AnyAsync(s => s.OsmId == station.Id))
                        {
                            stationToUpdate = await dbContext.Stations.FirstOrDefaultAsync(s => s.OsmId == station.Id);
                        }
                        else
                        {
                            stationToUpdate = new Station { OsmId = station.Id };
                            dbContext.Add(stationToUpdate);
                        }
                        stationToUpdate.Lattitude = station.Lat;
                        stationToUpdate.Longitude = station.Lon;
                        if (station.Tags.ContainsKey("name"))
                            stationToUpdate.Name = station.Tags["name"];
                        if (station.Tags.ContainsKey("ele"))
                            stationToUpdate.Elevation = double.Parse(station.Tags["ele"]);
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
            var query = $"[out:json][timeout:240];area({osmId})->.searchArea;(node[\"railway\"=\"station\"][!\"subway\"][!\"funicular\"][!\"tram\"][\"station\"!=\"monorail\"][\"station\"!=\"subway\"][\"station\"!=\"tram\"](area.searchArea);node[\"railway\"=\"station\"][\"train\"=\"yes\"](area.searchArea);node[\"railway\"=\"halt\"][!\"subway\"][!\"funicular\"][!\"tram\"][\"station\"!=\"monorail\"][\"station\"!=\"subway\"][\"station\"!=\"tram\"](area.searchArea);node[\"railway\"=\"halt\"][\"train\"=\"yes\"](area.searchArea););out body;";
            string text = null;
            using (var httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromSeconds(240);
                httpClient.DefaultRequestHeaders.Add("User-Agent", "OVDB");

                var response = await httpClient.PostAsync("https://overpass-api.de/api/interpreter", new StringContent(query));
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    return null;
                }
                text = await response.Content.ReadAsStringAsync();
            }

            return text;
        }

        [NonAction]
        public async Task<string> GetStationAsync(string osmId)
        {
            var query = $"[out:json][timeout:240];\r\nnode({osmId});out body;";
            string text = null;
            using (var httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromSeconds(240);
                httpClient.DefaultRequestHeaders.Add("User-Agent", "OVDB");

                var response = await httpClient.PostAsync("https://overpass-api.de/api/interpreter", new StringContent(query));
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    return null;
                }
                text = await response.Content.ReadAsStringAsync();
            }

            return text;
        }

        [NonAction]
        public async Task<string> GetStationWayList(string osmId)
        {
            var query = $"[out:json][timeout:240];area({osmId})->.searchArea;(way[\"railway\"=\"station\"][!\"subway\"][!\"funicular\"][!\"tram\"][\"station\"!=\"monorail\"][\"station\"!=\"subway\"][\"station\"!=\"tram\"](area.searchArea);node[\"railway\"=\"station\"][\"train\"=\"yes\"](area.searchArea);node[\"railway\"=\"halt\"][!\"subway\"][!\"funicular\"][!\"tram\"][\"station\"!=\"monorail\"][\"station\"!=\"subway\"][\"station\"!=\"tram\"](area.searchArea);way[\"railway\"=\"halt\"][\"train\"=\"yes\"](area.searchArea););out center;";
            string text = null;
            using (var httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromSeconds(240);
                httpClient.DefaultRequestHeaders.Add("User-Agent", "OVDB");

                var response = await httpClient.PostAsync("https://overpass-api.de/api/interpreter", new StringContent(query));
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    return null;
                }
                text = await response.Content.ReadAsStringAsync();
            }

            return text;
        }
    }
}
