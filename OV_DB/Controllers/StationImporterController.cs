using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OV_DB.Models;
using OV_DB.Services;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StationImporterController(OVDBDatabaseContext dbContext, IStationRegionsService stationRegionsService) : ControllerBase
    {
        [HttpPost("region/{regionId}")]
        public async Task<IActionResult> UpdateRegion(int regionId)
        {
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
            var stationOsmIdsFound = new List<long>();
            var stationsFound = new List<Station>();
            var region = await dbContext.Regions.FindAsync(regionId);
            var osmAreaId = (region.OsmRelationId + 3600_000_000).ToString();
            var list = await GetStationListAsync(osmAreaId);
            var tryCount = 1;
            while (list == null && tryCount < 6)
            {
                tryCount++;
                await Task.Delay((int)(10000 * (Math.Pow(2, tryCount))));
                list = await GetStationListAsync(osmAreaId);
            }

            var dbStations = await dbContext.Stations.Where(s => s.Regions.Any(r => r.Id == regionId)).Include(s => s.Regions).ToListAsync();
            var names = new List<string>();
            if (!string.IsNullOrWhiteSpace(list))
            {
                var parsedList = JsonConvert.DeserializeObject<OSMStationList>(list);


                parsedList.Elements.ForEach(station =>
                {
                    if (station.Tags.ContainsKey("name") && !string.IsNullOrWhiteSpace(station.Tags["name"]) && !(station.Lat == 0 && station.Lon == 0))
                    {
                        Station stationToUpdate = null;

                        if (dbStations.Any(s => s.OsmId == station.Id))
                        {
                            stationToUpdate = dbStations.FirstOrDefault(s => s.OsmId == station.Id);
                        }
                        else
                        {
                            stationToUpdate = new Station { OsmId = station.Id };
                            dbContext.Add(stationToUpdate);
                        }
                        stationsFound.Add(stationToUpdate);
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
                        names.Add(station.Tags["name"]);
                    }
                    stationOsmIdsFound.Add(station.Id);

                });
                await Task.Delay(1000);
            }

            var wayList = await GetStationWayList(osmAreaId);
            tryCount = 1;
            while (wayList == null && tryCount < 6)
            {
                tryCount++;
                await Task.Delay((int)(10000 * (Math.Pow(2, tryCount))));
                wayList = await GetStationWayList(osmAreaId);
            }

            if (!string.IsNullOrWhiteSpace(wayList))
            {
                var parsedWayList = JsonConvert.DeserializeObject<OSMStationWayList>(wayList);

                parsedWayList.Elements.ForEach(station =>
                {
                    if (station.Tags.ContainsKey("name") && !string.IsNullOrWhiteSpace(station.Tags["name"]) && station.Center != null && !(station.Center.Lat == 0 && station.Center.Lon == 0) && !names.Contains(station.Tags["name"]))
                    {
                        Station stationToUpdate = null;

                        if (dbStations.Any(s => s.OsmId == station.Id))
                        {
                            stationToUpdate = dbStations.FirstOrDefault(s => s.OsmId == station.Id);
                        }
                        else
                        {
                            stationToUpdate = new Station { OsmId = station.Id };
                            dbContext.Add(stationToUpdate);
                        }
                        stationsFound.Add(stationToUpdate);
                        stationToUpdate.Lattitude = station.Center.Lat;
                        stationToUpdate.Longitude = station.Center.Lon;
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
                    }
                    stationOsmIdsFound.Add(station.Id);
                });
                await Task.Delay(1000);
            }
            if (list != null && wayList != null)
            {
                var stationsToDisable = await dbContext.Stations.Where(s => s.Regions.Any(r => r.Id == regionId) && !stationOsmIdsFound.Contains(s.OsmId)).ToListAsync();
                stationsToDisable.ForEach(station =>
                {
                    station.Hidden = true;
                });
            }

            foreach (var station in stationsFound)
            {
                await stationRegionsService.AssignRegionsToStationCacheRegionsAsync(station);
            }

            await dbContext.SaveChangesAsync();

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
