using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OV_DB.Hubs;
using OVDB_database.Database;

namespace OV_DB.Services
{
    /// <summary>
    /// Runs the full-history resyncs the Träwelling page asks for. Walking a whole history
    /// takes minutes — longer than any request survives — so the endpoint only queues, and
    /// the page hears about it over the hub.
    /// </summary>
    public class TraewellingResyncService(
        IServiceProvider serviceProvider,
        ITraewellingResyncQueue queue,
        IHubContext<TraewellingHub> hubContext,
        ILogger<TraewellingResyncService> logger) : BackgroundService
    {
        // A page at a time is too chatty for a thousand-page walk and says nothing new.
        private const int ProgressEveryPages = 5;

        /// <remarks>
        /// Not <see cref="Progress{T}"/>: that posts each report to the thread pool, which
        /// reorders them and races the counter this throttles on.
        /// </remarks>
        private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
        {
            public void Report(T value) => report(value);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var userId in queue.DequeueAllAsync(stoppingToken))
            {
                try
                {
                    await ResyncAsync(userId, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Error resyncing Träwelling history for user {UserId}", userId);
                    await PublishAsync(userId, TraewellingHub.ResyncFinishedMethod,
                        new { added = 0, pagesRead = 0, complete = false });
                }
                finally
                {
                    queue.MarkFinished(userId);
                }
            }
        }

        private async Task ResyncAsync(int userId, CancellationToken cancellationToken)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OVDBDatabaseContext>();
            var trawellingService = scope.ServiceProvider.GetRequiredService<ITrawellingService>();

            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null || !trawellingService.IsConnected(user))
            {
                await PublishAsync(userId, TraewellingHub.ResyncFinishedMethod,
                    new { added = 0, pagesRead = 0, complete = false });
                return;
            }

            var lastReported = 0;
            var progress = new InlineProgress<TrawellingSweepProgress>(p =>
            {
                if (p.PagesRead - lastReported < ProgressEveryPages)
                    return;
                lastReported = p.PagesRead;
                // Not awaited: the walk should not wait on a hub delivery, and a failed one
                // is logged and forgotten — the finish event carries the totals anyway.
                _ = PublishAsync(userId, TraewellingHub.ResyncProgressMethod,
                    new { pagesRead = p.PagesRead, added = p.Added });
            });

            var result = await trawellingService.SweepInboxAsync(user, TrawellingSweepMode.Full, progress, cancellationToken);

            logger.LogInformation("Träwelling resync for user {UserId}: {Added} added over {Pages} pages, complete {Complete}",
                userId, result.Added, result.PagesRead, result.Success && result.ReachedEnd);

            await PublishAsync(userId, TraewellingHub.ResyncFinishedMethod, new
            {
                added = result.Added,
                pagesRead = result.PagesRead,
                complete = result.Success && result.ReachedEnd
            });
        }

        private async Task PublishAsync(int userId, string method, object payload)
        {
            try
            {
                await hubContext.Clients.User(userId.ToString())
                    .SendAsync(method, JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error publishing Träwelling resync {Method} for user {UserId}", method, userId);
            }
        }
    }
}
