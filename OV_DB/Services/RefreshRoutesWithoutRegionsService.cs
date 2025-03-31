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
    public class RefreshRoutesWithoutRegionsService : IHostedService, IDisposable
    {
        private readonly OVDBDatabaseContext _dbContext;
        private readonly IRouteRegionsService _routeRegionsService;
        private readonly IHubContext<MapGenerationHub> _hubContext;
        private readonly ConcurrentQueue<int> _routeQueue = new ConcurrentQueue<int>();
        private Task _executingTask;
        private CancellationTokenSource _cts;

        public RefreshRoutesWithoutRegionsService(OVDBDatabaseContext dbContext, IRouteRegionsService routeRegionsService, IHubContext<MapGenerationHub> hubContext)
        {
            _dbContext = dbContext;
            _routeRegionsService = routeRegionsService;
            _hubContext = hubContext;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _executingTask = ExecuteAsync(_cts.Token);
            return Task.CompletedTask;
        }

        public void EnqueueRouteRefresh(int routeId)
        {
            _routeQueue.Enqueue(routeId);
        }

        private async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_routeQueue.TryDequeue(out var routeId))
                {
                    await RefreshRoutesWithoutRegionsAsync();
                }
                await Task.Delay(10000, cancellationToken);
            }
        }

        public async Task RefreshRoutesWithoutRegionsAsync()
        {
            var routes = _dbContext.Routes.Where(r => r.RegionId == null).ToList();
            var totalRoutes = routes.Count;
            var processedRoutes = 0;

            foreach (var route in routes)
            {
                await _routeRegionsService.AssignRegionsToRouteAsync(route);
                processedRoutes++;
                var progress = (processedRoutes * 100) / totalRoutes;
                await _hubContext.Clients.All.SendAsync("GenerationUpdate", "RefreshRoutesWithoutRegions", progress);
            }

            await _dbContext.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("GenerationUpdate", "RefreshRoutesWithoutRegions", 100);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_executingTask == null)
            {
                return;
            }

            _cts.Cancel();

            await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }

        public void Dispose()
        {
            _cts?.Cancel();
        }
    }
}
