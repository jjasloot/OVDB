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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OV_DB.Models;
using Microsoft.Extensions.Logging;

namespace OV_DB.Services
{
    public class RefreshRoutesService(IServiceProvider serviceProvider, IHubContext<MapGenerationHub> hubContext, ILogger<RefreshRoutesService> logger) : IHostedService, IDisposable
    {
        public static readonly ConcurrentQueue<int> RouteQueue = new ConcurrentQueue<int>();
        private Task _backgroundTask;
        private CancellationTokenSource _cancellationTokenSource;

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
                if (RouteQueue.TryDequeue(out var routeId))
                {
                    try
                    {
                        await RefreshRoutesAsync(routeId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error refreshing routes for region {RegionId}", routeId);
                    }
                }
                else
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        public async Task RefreshRoutesAsync(int regionId)
        {
            using var scope = serviceProvider.CreateScope();
            using var dbContext = scope.ServiceProvider.GetService<OVDBDatabaseContext>();
            var routeRegionsService = scope.ServiceProvider.GetService<IRouteRegionsService>();

            var routes = await dbContext.Routes.Where(r => r.Regions.Any(r => r.Id == regionId)).Include(r=>r.Regions).ToListAsync();
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
                    await hubContext.Clients.All.SendAsync(MapGenerationHub.RegionUpdateMethod, regionId, progress);

                }
            }

            await dbContext.SaveChangesAsync();

            await hubContext.Clients.All.SendAsync(MapGenerationHub.RegionUpdateMethod, regionId, 100, updatedRoutes);
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
