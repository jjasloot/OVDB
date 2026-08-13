using System;
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
    public class TraewellingInboxSweepService(IServiceProvider serviceProvider, ILogger<TraewellingInboxSweepService> logger) : BackgroundService
    {
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
                if (await trawellingService.SweepInboxAsync(user, force: true, cancellationToken))
                    swept++;
                // Daily job, so latency is free — don't burst through the shared rate limit
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }

            logger.LogInformation("Träwelling inbox sweep: {Swept}/{Total} users swept", swept, connectedUsers.Count);
        }
    }
}
