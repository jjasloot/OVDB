using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.SignalR;
using OV_DB.Hubs;
using OVDB_database.Database;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace OV_DB.Services
{
    public class RefreshRoutesWithoutRegionsService(IServiceProvider serviceProvider, IHubContext<MapGenerationHub> hubContext, ILogger<RefreshRoutesWithoutRegionsService> logger) : IHostedService, IDisposable
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
                        await RefreshRoutesWithoutRegionsAsync(cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        // Log instead of silently swallowing, and release the UI which waits for 100%.
                        // The SignalR call is itself guarded so a hub failure can't kill the loop.
                        logger.LogError(ex, "Error refreshing routes without regions");
                        try
                        {
                            await hubContext.Clients.All.SendAsync(MapGenerationHub.RegionUpdateMethod, 0, 100, 0, cancellationToken);
                        }
                        catch (Exception hubEx)
                        {
                            logger.LogWarning(hubEx, "Failed to notify clients after a routes-without-regions failure");
                        }
                    }
                }
                else
                {
                    // Only idle-wait when there was nothing to do, instead of after every item.
                    try
                    {
                        await Task.Delay(10000, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        public async Task RefreshRoutesWithoutRegionsAsync(CancellationToken cancellationToken = default)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<OVDBDatabaseContext>();
            var routeRegionsService = scope.ServiceProvider.GetService<IRouteRegionsService>();

            var routes = await dbContext.Routes.Where(r => !r.Regions.Any()).Include(r => r.Regions).ToListAsync(cancellationToken);
            var totalRoutes = routes.Count;
            if (totalRoutes == 0)
            {
                await hubContext.Clients.All.SendAsync(MapGenerationHub.RegionUpdateMethod, 0, 100, 0, cancellationToken);
                return;
            }
            var processedRoutes = 0;
            var progress = 0;
            var updatedRoutes = 0;
            foreach (var route in routes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var updated = await routeRegionsService.AssignRegionsToRouteAsync(route);
                if (updated) updatedRoutes += 1;
                processedRoutes++;
                var newProgress = (processedRoutes * 98) / totalRoutes;
                if (newProgress != progress)
                {
                    progress = newProgress;
                    await hubContext.Clients.All.SendAsync(MapGenerationHub.RegionUpdateMethod, 0, progress, cancellationToken: cancellationToken);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            await hubContext.Clients.All.SendAsync(MapGenerationHub.RegionUpdateMethod, 0, 100, updatedRoutes, cancellationToken);
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
