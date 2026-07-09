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
    public class TraewellingTokenRefreshService(IServiceProvider serviceProvider, ILogger<TraewellingTokenRefreshService> logger) : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RefreshIdleConnectionsAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Error while refreshing Träwelling tokens");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }

        private async Task RefreshIdleConnectionsAsync(CancellationToken cancellationToken)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OVDBDatabaseContext>();
            var trawellingService = scope.ServiceProvider.GetRequiredService<ITrawellingService>();

            // Users with an expired access token haven't used the integration recently;
            // refreshing rotates their refresh token, which resets its 30-day expiry
            var idleUsers = await dbContext.Users
                .Where(u => u.TrawellingRefreshToken != null && u.TrawellingRefreshToken != ""
                    && (u.TrawellingTokenExpiresAt == null || u.TrawellingTokenExpiresAt < DateTime.UtcNow))
                .ToListAsync(cancellationToken);

            if (idleUsers.Count == 0)
                return;

            var refreshed = 0;
            foreach (var user in idleUsers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await trawellingService.EnsureValidTokenAsync(user))
                    refreshed++;
            }

            logger.LogInformation("Träwelling token refresh: {Refreshed}/{Total} idle connections refreshed",
                refreshed, idleUsers.Count);
        }
    }
}
