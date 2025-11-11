using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using OV_DB.Hubs;
using OV_DB.Models;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OV_DB.Services
{
    public class UpdateRegionService(IServiceProvider serviceProvider, IHubContext<MapGenerationHub> hubContext, IHttpClientFactory httpClientFactory) : IHostedService, IDisposable
    {
        public static readonly ConcurrentQueue<int> RegionQueue = new ConcurrentQueue<int>();
        private Task _backgroundTask;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _backgroundTask = Task.Run(() => ProcessQueueAsync(_cancellationTokenSource.Token));
            return Task.CompletedTask;
        }

        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (RegionQueue.TryDequeue(out var regionId))
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
            using var scope = serviceProvider.CreateScope();
            using var dbContext = scope.ServiceProvider.GetService<OVDBDatabaseContext>();
            var stationRegionsService = scope.ServiceProvider.GetService<IStationRegionsService>();


            var regionOSMId = await dbContext.Regions.Where(r => r.Id == regionId).Select(r => r.OsmRelationId).FirstAsync() + 3600_000_000;
            var list = await GetStationListAsync(regionOSMId);
            var tryCount = 1;
            while (list == null && tryCount < 6)
            {
                tryCount++;
                await Task.Delay((int)(10000 * (Math.Pow(2, tryCount))));
                list = await GetStationListAsync(regionOSMId);
            }

            if (!string.IsNullOrWhiteSpace(list))
            {
                await hubContext.Clients.All.SendAsync(MapGenerationHub.RegionStationUpdateMethod, regionId, 20);
                var parsedList = JsonConvert.DeserializeObject<OSMStationList>(list);

                var processedStations = 0;
                var progress = 0;
                var totalCount = parsedList.Elements.Count;
                foreach (var station in parsedList.Elements)
                {
                    if (station.Tags.ContainsKey("name") && !string.IsNullOrWhiteSpace(station.Tags["name"]) && !(station.Lat == 0 && station.Lon == 0))
                    {
                        Station stationToUpdate = null;

                        if (await dbContext.Stations.AnyAsync(s => s.OsmId == station.Id))
                        {
                            stationToUpdate = await dbContext.Stations.Include(s => s.Regions).FirstOrDefaultAsync(s => s.OsmId == station.Id);
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

                    processedStations++;
                    var newProgress = ((processedStations * 78) / totalCount) + 20;
                    if (newProgress != progress)
                    {
                        progress = newProgress;
                        await hubContext.Clients.All.SendAsync(MapGenerationHub.RegionStationUpdateMethod, regionId, progress);

                    }
                }
            }
            await dbContext.SaveChangesAsync();

            await hubContext.Clients.All.SendAsync(MapGenerationHub.RegionStationUpdateMethod, regionId, 100);
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

        public async Task<string> GetStationListAsync(long osmId)
        {
            var query = $"[out:json][timeout:240];area({osmId})->.searchArea;(node[\"railway\"=\"station\"][!\"subway\"][!\"funicular\"][!\"tram\"][\"station\"!=\"monorail\"][\"station\"!=\"subway\"][\"station\"!=\"tram\"](area.searchArea);node[\"railway\"=\"station\"][\"train\"=\"yes\"](area.searchArea);node[\"railway\"=\"halt\"][!\"subway\"][!\"funicular\"][!\"tram\"][\"station\"!=\"monorail\"][\"station\"!=\"subway\"][\"station\"!=\"tram\"](area.searchArea);node[\"railway\"=\"halt\"][\"train\"=\"yes\"](area.searchArea););out body;";
            string text = null;
            var httpClient = _httpClientFactory.CreateClient("OSM");

            using (var content = new StringContent(query))
            {
                var response = await httpClient.PostAsync("https://overpass-api.de/api/interpreter", content);
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
