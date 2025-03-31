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
using System.Collections.Concurrent;

namespace OV_DB.Services
{
    public class UpdateRegionService : IHostedService, IDisposable
    {
        private readonly OVDBDatabaseContext _dbContext;
        private readonly IStationRegionsService _stationRegionsService;
        private readonly IHubContext<MapGenerationHub> _hubContext;
        private readonly ConcurrentQueue<int> _regionQueue = new ConcurrentQueue<int>();
        private Task _backgroundTask;
        private CancellationTokenSource _cancellationTokenSource;

        public UpdateRegionService(OVDBDatabaseContext dbContext, IStationRegionsService stationRegionsService, IHubContext<MapGenerationHub> hubContext)
        {
            _dbContext = dbContext;
            _stationRegionsService = stationRegionsService;
            _hubContext = hubContext;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _backgroundTask = Task.Run(() => ProcessQueueAsync(_cancellationTokenSource.Token));
            return Task.CompletedTask;
        }

        public void EnqueueRegionUpdate(int regionId)
        {
            _regionQueue.Enqueue(regionId);
        }

        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_regionQueue.TryDequeue(out var regionId))
                {
                    await UpdateRegionAsync(regionId);
                }
                else
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        public async Task UpdateRegionAsync(int regionId)
        {
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
            _cancellationTokenSource.Cancel();
            return Task.WhenAny(_backgroundTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
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
