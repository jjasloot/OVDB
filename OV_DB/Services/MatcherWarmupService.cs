using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OV_DB.Hubs;

namespace OV_DB.Services
{
    /// <summary>
    /// Builds the route index the backfill queue is about to lean on, before the first station
    /// is asked for.
    /// </summary>
    /// <remarks>
    /// The build reads every route's geometry and takes seconds, and it used to happen inside
    /// whichever request hit a cold index first — an unexplained stall, and long enough to be at
    /// the mercy of a proxy timeout. Opening the backfill page is the moment we know it is coming,
    /// so it is started here instead and the page watches it fill.
    /// </remarks>
    public class MatcherWarmupService(
        IServiceProvider serviceProvider,
        IMatcherWarmupQueue queue,
        IHubContext<StationsHub> hubContext,
        ILogger<MatcherWarmupService> logger) : BackgroundService
    {
        /// <summary>Every route would be thousands of messages to say the same thing.</summary>
        private const int ProgressEveryRoutes = 250;

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
                    await WarmAsync(userId, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Error warming the route index for user {UserId}", userId);
                    // Told either way: the page waits on this message to start loading, and a
                    // failed build must not leave it waiting forever. The first station request
                    // will try the build again and, if it fails too, fail honestly.
                    await PublishAsync(userId, StationsHub.WarmupFinishedMethod, new { ready = false });
                }
                finally
                {
                    queue.MarkFinished(userId);
                }
            }
        }

        private async Task WarmAsync(int userId, CancellationToken cancellationToken)
        {
            using var scope = serviceProvider.CreateScope();
            var matcher = scope.ServiceProvider.GetRequiredService<IStationTripMatcher>();

            var lastReported = 0;
            var progress = new InlineProgress<MatcherWarmupProgress>(p =>
            {
                // The last report always goes through, so the bar reaches its end rather than
                // stopping a few hundred routes short and jumping.
                if (p.Processed < p.Total && p.Processed - lastReported < ProgressEveryRoutes)
                    return;
                lastReported = p.Processed;
                // Not awaited: the build should not wait on a hub delivery, and a dropped
                // progress message costs nothing — the finished event is what the page acts on.
                _ = PublishAsync(userId, StationsHub.WarmupProgressMethod,
                    new { processed = p.Processed, total = p.Total });
            });

            var started = DateTime.UtcNow;
            await matcher.WarmRouteIndexAsync(progress, cancellationToken);
            logger.LogInformation("Route index warmed for user {UserId} in {Seconds:F1}s",
                userId, (DateTime.UtcNow - started).TotalSeconds);

            await PublishAsync(userId, StationsHub.WarmupFinishedMethod, new { ready = true });
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
                logger.LogError(ex, "Error publishing route index {Method} for user {UserId}", method, userId);
            }
        }
    }
}
