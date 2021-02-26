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
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StationImporterController : ControllerBase
    {
        private OVDBDatabaseContext DbContext { get; }
        public StationImporterController(OVDBDatabaseContext dbContext)
        {
            DbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> UpdateAllCountries()
        {
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
            var countries = await DbContext.StationCountries.ToListAsync();
            foreach (var country in countries)
            {
                var stationOsmIdsFound = new List<long>();
                var list = await GetStationList(country.OsmId);
                var tryCount = 1;
                while (list == null && tryCount < 6)
                {
                    tryCount++;
                    await Task.Delay((int)(10000 * (Math.Pow(2, tryCount))));
                    list = await GetStationList(country.OsmId);
                }

                var dbStations = await DbContext.Stations.Where(s => s.StationCountryId == country.Id).ToListAsync();
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
                                stationToUpdate = new Station { OsmId = station.Id, StationCountryId = country.Id };
                                DbContext.Add(stationToUpdate);
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
                            names.Add(station.Tags["name"]);
                        }
                        stationOsmIdsFound.Add(station.Id);
                    });
                    await Task.Delay(1000);
                }

                var wayList = await GetStationWayList(country.OsmId);
                tryCount = 1;
                while (wayList == null && tryCount<6)
                {
                    tryCount++;
                    await Task.Delay((int)(10000 * (Math.Pow(2, tryCount))));
                    wayList = await GetStationWayList(country.OsmId);
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
                                stationToUpdate = new Station { OsmId = station.Id, StationCountryId = country.Id };
                                DbContext.Add(stationToUpdate);
                            }
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
                    var stationsToDisable = await DbContext.Stations.Where(s => s.StationCountryId == country.Id && !stationOsmIdsFound.Contains(s.OsmId)).ToListAsync();
                    stationsToDisable.ForEach(station =>
                    {
                        station.Hidden = true;
                    });
                }
            }
            await DbContext.SaveChangesAsync();

            return Ok();
        }

        public async Task<string> GetStationList(string osmId)
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
