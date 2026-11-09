using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.SignalR;
using OV_DB.Hubs;
using OVDB_database.Database;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OV_DB.Services
{
    /// <summary>
    /// Recalculates the regions of a batch of routes in the background, reporting progress over
    /// the map-generation hub.
    /// </summary>
    public class RefreshRoutesService(IServiceProvider serviceProvider, IHubContext<MapGenerationHub> hubContext, ILogger<RefreshRoutesService> logger) : IHostedService, IDisposable
    {
        /// <summary>Queue item for "every route that has no region yet" (region ids start at 1).</summary>
        public const int RoutesWithoutRegions = 0;

        /// <summary>Region ids whose routes to refresh, or <see cref="RoutesWithoutRegions"/>.</summary>
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
                if (RouteQueue.TryDequeue(out var regionId))
                {
                    try
                    {
                        await RefreshRoutesAsync(regionId, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        // Never let one failure kill the queue loop for good, and release the UI,
                        // which waits for 100%. The hub call is guarded for the same reason.
                        logger.LogError(ex, "Error refreshing routes for region {RegionId}; continuing with the queue", regionId);
                        try
                        {
                            await hubContext.Clients.All.SendAsync(MapGenerationHub.RegionUpdateMethod, regionId, 100, 0, cancellationToken);
                        }
                        catch (Exception hubEx)
                        {
                            logger.LogWarning(hubEx, "Failed to notify clients after a route refresh failure");
                        }
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

        public async Task RefreshRoutesAsync(int regionId, CancellationToken cancellationToken = default)
        {
            // The scope owns the resolved DbContext, so it is disposed with the scope; do not
            // dispose it separately.
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<OVDBDatabaseContext>();
            var routeRegionsService = scope.ServiceProvider.GetService<IRouteRegionsService>();

            IQueryable<OVDB_database.Models.Route> query;
            if (regionId == RoutesWithoutRegions)
            {
                query = dbContext.Routes.Where(r => !r.Regions.Any());
            }
            else
            {
                // Not just the routes already in the region: a region that was just added has
                // none yet, so routes crossing it count too; AssignRegionsToRouteAsync decides per
                // route. (Taking the parent region's routes instead loads a whole country's
                // geometry and times out.)
                var geometry = await dbContext.Regions.AsNoTracking()
                    .Where(r => r.Id == regionId)
                    .Select(r => r.Geometry)
                    .SingleAsync(cancellationToken);
                query = dbContext.Routes.Where(r => r.Regions.Any(x => x.Id == regionId) || (r.LineString != null && r.LineString.Intersects(geometry)));
            }
            var routes = await query.Include(r => r.Regions).ToListAsync(cancellationToken);
            var totalRoutes = routes.Count;
            if (totalRoutes == 0)
            {
                await hubContext.Clients.All.SendAsync(MapGenerationHub.RegionUpdateMethod, regionId, 100, 0, cancellationToken);
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
                    await hubContext.Clients.All.SendAsync(MapGenerationHub.RegionUpdateMethod, regionId, progress, cancellationToken: cancellationToken);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            await hubContext.Clients.All.SendAsync(MapGenerationHub.RegionUpdateMethod, regionId, 100, updatedRoutes, cancellationToken);
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
