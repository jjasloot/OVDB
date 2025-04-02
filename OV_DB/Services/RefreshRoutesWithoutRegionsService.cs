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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace OV_DB.Services
{
    public class RefreshRoutesWithoutRegionsService(IServiceProvider serviceProvider, IHubContext<MapGenerationHub> hubContext) : IHostedService, IDisposable
    {
        public static readonly ConcurrentQueue<bool> RouteQueue = new ConcurrentQueue<bool>();
        private Task _executingTask;
        private CancellationTokenSource _cts;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _executingTask = ExecuteAsync(_cts.Token);
            return Task.CompletedTask;
        }

        private async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (RouteQueue.TryDequeue(out _))
                {
                    try
                    {
                        await RefreshRoutesWithoutRegionsAsync();
                    }
                    catch { }
                }
                await Task.Delay(10000, cancellationToken);
            }
        }

        public async Task RefreshRoutesWithoutRegionsAsync()
        {
            using var scope = serviceProvider.CreateScope();
            using var dbContext = scope.ServiceProvider.GetService<OVDBDatabaseContext>();
            var routeRegionsService = scope.ServiceProvider.GetService<IRouteRegionsService>();

            var routes = dbContext.Routes.Where(r => !r.Regions.Any()).Include(r=>r.Regions).ToList();
            var totalRoutes = routes.Count;
            var processedRoutes = 0;
            var progress = 0;
            var updatedRoutes = 0;
            foreach (var route in routes)
            {
                var updated = await routeRegionsService.AssignRegionsToRouteAsync(route);
                if (updated) updatedRoutes += 1;
                processedRoutes++;
                var newProgress = (processedRoutes * 98) / totalRoutes;
                if (newProgress != progress)
                {
                    progress = newProgress;
                    await hubContext.Clients.All.SendAsync(MapGenerationHub.RegionUpdateMethod, 0, progress);

                }
            }

            await dbContext.SaveChangesAsync();

            await hubContext.Clients.All.SendAsync(MapGenerationHub.RegionUpdateMethod,0, 100, updatedRoutes);
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
