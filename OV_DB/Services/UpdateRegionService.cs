using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using OV_DB.Hubs;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Services
{
    public class UpdateRegionService : IHostedService, IDisposable
    {
        private readonly OVDBDatabaseContext _dbContext;
        private readonly IStationRegionsService _stationRegionsService;
        private readonly IHubContext<MapGenerationHub> _hubContext;
        private Timer _timer;

        public UpdateRegionService(OVDBDatabaseContext dbContext, IStationRegionsService stationRegionsService, IHubContext<MapGenerationHub> hubContext)
        {
            _dbContext = dbContext;
            _stationRegionsService = stationRegionsService;
            _hubContext = hubContext;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(UpdateRegion, null, TimeSpan.Zero, TimeSpan.FromHours(1));
            return Task.CompletedTask;
        }

        private async void UpdateRegion(object state)
        {
            var regionId = (int)state;
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var list = await GetStationListAsync(regionId.ToString());
            var tryCount = 1;
            while (list == null && tryCount < 6)
            {
                tryCount++;
                await Task.Delay((int)(10000 * (Math.Pow(2, tryCount))));
                list = await GetStationListAsync(regionId.ToString());
            }

            if (!string.IsNullOrWhiteSpace(list))
            {
                var parsedList = JsonConvert.DeserializeObject<OSMStationList>(list);

                foreach (var station in parsedList.Elements)
                {
                    if (station.Tags.ContainsKey("name") && !string.IsNullOrWhiteSpace(station.Tags["name"]) && !(station.Lat == 0 && station.Lon == 0))
                    {
                        Station stationToUpdate = null;

                        if (await _dbContext.Stations.AnyAsync(s => s.OsmId == station.Id))
                        {
                            stationToUpdate = await _dbContext.Stations.FirstOrDefaultAsync(s => s.OsmId == station.Id);
                        }
                        else
                        {
                            stationToUpdate = new Station { OsmId = station.Id };
                            _dbContext.Add(stationToUpdate);
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
                        await _stationRegionsService.AssignRegionsToStationCacheRegionsAsync(stationToUpdate);
                    }
                }
            }
            await _dbContext.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("GenerationUpdate", regionId, 100);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
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
    }
}
