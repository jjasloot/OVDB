using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OV_DB.Hubs;
using OV_DB.Models;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OV_DB.Services
{
    public class UpdateRegionService(IServiceProvider serviceProvider, IHubContext<MapGenerationHub> hubContext, IOverpassService overpassService, ILogger<UpdateRegionService> logger) : IHostedService, IDisposable
    {
        public static readonly ConcurrentQueue<int> RegionQueue = new ConcurrentQueue<int>();
        private Task _backgroundTask;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly IOverpassService _overpassService = overpassService;

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
                    try
                    {
                        await UpdateRegionAsync(regionId, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        // Never let one region's failure kill the queue loop for good.
                        logger.LogError(ex, "Failed to update region {RegionId}; continuing with the queue", regionId);
                        await hubContext.Clients.All.SendAsync(MapGenerationHub.RegionStationUpdateMethod, regionId, 100, cancellationToken);
                    }
                }
                else
                {
                    try
                    {
                        await Task.Delay(1000, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        public async Task UpdateRegionAsync(int regionId, CancellationToken cancellationToken = default)
        {
            using var scope = serviceProvider.CreateScope();
            using var dbContext = scope.ServiceProvider.GetService<OVDBDatabaseContext>();
            var stationRegionsService = scope.ServiceProvider.GetService<IStationRegionsService>();


            var regionOSMId = await dbContext.Regions.Where(r => r.Id == regionId).Select(r => r.OsmRelationId).FirstAsync(cancellationToken) + 3600_000_000;
            var list = await GetStationListAsync(regionOSMId, cancellationToken);
            // Each attempt already fails over across all mirrors; the delays just need to
            // outlast a typical endpoint cooldown, not implement backoff themselves.
            var tryCount = 0;
            while (list == null && tryCount < 3)
            {
                tryCount++;
                await Task.Delay(TimeSpan.FromSeconds(30 * tryCount), cancellationToken);
                list = await GetStationListAsync(regionOSMId, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(list))
            {
                await hubContext.Clients.All.SendAsync(MapGenerationHub.RegionStationUpdateMethod, regionId, 20);
                var parsedList = JsonConvert.DeserializeObject<OSMStationList>(list);

                // One roundtrip for all existing stations instead of two queries per station.
                var osmIds = parsedList.Elements.Select(e => e.Id).ToList();
                var existingStations = await dbContext.Stations.Include(s => s.Regions)
                    .Where(s => osmIds.Contains(s.OsmId))
                    .ToDictionaryAsync(s => s.OsmId, cancellationToken);

                var processedStations = 0;
                var progress = 0;
                var totalCount = parsedList.Elements.Count;
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

                    processedStations++;
                    var newProgress = ((processedStations * 78) / totalCount) + 20;
                    if (newProgress != progress)
                    {
                        progress = newProgress;
                        await hubContext.Clients.All.SendAsync(MapGenerationHub.RegionStationUpdateMethod, regionId, progress);

                    }
                }
            }
            await dbContext.SaveChangesAsync(cancellationToken);

            await hubContext.Clients.All.SendAsync(MapGenerationHub.RegionStationUpdateMethod, regionId, 100, cancellationToken);
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

        public async Task<string> GetStationListAsync(long osmId, CancellationToken cancellationToken = default)
        {
            var query = $"[out:json][timeout:180];area({osmId})->.searchArea;(node[\"railway\"=\"station\"][!\"subway\"][!\"funicular\"][!\"tram\"][\"station\"!=\"monorail\"][\"station\"!=\"subway\"][\"station\"!=\"tram\"](area.searchArea);node[\"railway\"=\"station\"][\"train\"=\"yes\"](area.searchArea);node[\"railway\"=\"halt\"][!\"subway\"][!\"funicular\"][!\"tram\"][\"station\"!=\"monorail\"][\"station\"!=\"subway\"][\"station\"!=\"tram\"](area.searchArea);node[\"railway\"=\"halt\"][\"train\"=\"yes\"](area.searchArea););out body;";
            return await _overpassService.QueryAsync(query, cancellationToken);
        }
    }
}
