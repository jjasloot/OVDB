using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OVDB_database.Database;

namespace OV_DB.Services
{
    /// <summary>
    /// Daily reconciliation sweep of the Träwelling inbox for every connected user, so the
    /// unimported-trips list stays current even for users who rarely open it. Interactive
    /// use gets fresher data via the stale-check in GetOptimizedTripsAsync.
    /// </summary>
    public class TraewellingInboxSweepService(IServiceProvider serviceProvider, ITraewellingResyncQueue resyncQueue, ILogger<TraewellingInboxSweepService> logger) : BackgroundService
    {
        // shortcut: remembered in memory, so a user is queued for the UUID backfill at most once per app
        // start. Trips whose status was deleted on Träwelling never get a UUID; persisting this would
        // save one resync per restart for their owners, worth doing only if restarts become frequent.
        private readonly ConcurrentDictionary<int, bool> _uuidBackfillQueued = new();

        private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SweepAllUsersAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Error while sweeping Träwelling inboxes");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }

        private async Task SweepAllUsersAsync(CancellationToken cancellationToken)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OVDBDatabaseContext>();
            var trawellingService = scope.ServiceProvider.GetRequiredService<ITrawellingService>();

            var connectedUsers = await dbContext.Users
                .Where(u => u.TrawellingRefreshToken != null && u.TrawellingRefreshToken != "")
                .ToListAsync(cancellationToken);

            if (connectedUsers.Count == 0)
                return;

            var swept = 0;
            foreach (var user in connectedUsers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if ((await trawellingService.SweepInboxAsync(user, TrawellingSweepMode.Force, cancellationToken: cancellationToken)).Success)
                    swept++;

                // Imports from before status UUIDs existed get theirs from one full resync, which the
                // queue runs in the background, one user at a time, within the shared rate limit.
                if (!_uuidBackfillQueued.ContainsKey(user.Id)
                    && await NeedsUuidBackfillAsync(dbContext, user.Id, cancellationToken)
                    && resyncQueue.TryEnqueue(user.Id))
                {
                    _uuidBackfillQueued[user.Id] = true;
                    logger.LogInformation("Queued a full Träwelling resync for user {UserId} to fill in status UUIDs", user.Id);
                }
                // Daily job, so latency is free — don't burst through the shared rate limit
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }

            logger.LogInformation("Träwelling inbox sweep: {Swept}/{Total} users swept", swept, connectedUsers.Count);
        }

        /// <summary>Whether this user has imported trips that still lack their Träwelling status UUID.</summary>
        internal static Task<bool> NeedsUuidBackfillAsync(OVDBDatabaseContext dbContext, int userId, CancellationToken cancellationToken) =>
            dbContext.RouteInstances
                .Where(ri => ri.TrawellingStatusId.HasValue && ri.TrawellingStatusUuid == null)
                .Where(ri => ri.RouteInstanceMaps.Any(rim => rim.Map.UserId == userId)
                    || ri.Route.RouteMaps.Any(rm => rm.Map.UserId == userId))
                .AnyAsync(cancellationToken);
    }
}
