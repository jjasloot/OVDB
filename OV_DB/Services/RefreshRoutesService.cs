using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.SignalR;
using OV_DB.Hubs;
using OVDB_database.Database;
using OVDB_database.Models;
using System.Collections.Concurrent;

namespace OV_DB.Services
{
    public class RefreshRoutesService : IHostedService, IDisposable
    {
        private readonly OVDBDatabaseContext _dbContext;
        private readonly IRouteRegionsService _routeRegionsService;
        private readonly IHubContext<MapGenerationHub> _hubContext;
        private readonly ConcurrentQueue<int> _routeQueue = new ConcurrentQueue<int>();
        private Task _backgroundTask;
        private CancellationTokenSource _cancellationTokenSource;

        public RefreshRoutesService(OVDBDatabaseContext dbContext, IRouteRegionsService routeRegionsService, IHubContext<MapGenerationHub> hubContext)
        {
            _dbContext = dbContext;
            _routeRegionsService = routeRegionsService;
            _hubContext = hubContext;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _backgroundTask = Task.Run(() => ProcessQueueAsync(_cancellationTokenSource.Token));
            return Task.CompletedTask;
        }

        public void EnqueueRouteRefresh(int routeId)
        {
            _routeQueue.Enqueue(routeId);
        }

        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_routeQueue.TryDequeue(out var routeId))
                {
                    await RefreshRoutesAsync(routeId);
                }
                else
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        public async Task RefreshRoutesAsync(int regionId)
        {
            var routes = _dbContext.Routes.Where(r => r.RegionId == regionId).ToList();
            var totalRoutes = routes.Count;
            var processedRoutes = 0;

            foreach (var route in routes)
            {
                await _routeRegionsService.AssignRegionsToRouteAsync(route);
                processedRoutes++;
                var progress = (processedRoutes * 100) / totalRoutes;
                await _hubContext.Clients.All.SendAsync("GenerationUpdate", regionId, progress);
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
    }
}
