using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OV_DB.Services
{
    public interface ITraewellingRateLimiter
    {
        /// <summary>True while the shared rate-limit budget is exhausted.</summary>
        bool IsLimited { get; }
        /// <summary>Delays until the rate-limit window has reset, if currently limited.</summary>
        Task WaitIfLimitedAsync(CancellationToken cancellationToken = default);
        /// <summary>Records x-ratelimit-* headers and 429/503 Retry-After from a Träwelling response.</summary>
        void RecordResponse(HttpResponseMessage response);
    }

    /// <summary>
    /// Shared (singleton) view of the Träwelling API rate limit. TrawellingService itself is
    /// scoped, so any state kept there dies with the request scope; this keeps the remaining
    /// budget visible across concurrent imports, the token-refresh background job and the
    /// OAuth flow, which all draw from the same per-client limit.
    /// </summary>
    public class TraewellingRateLimiter(ILogger<TraewellingRateLimiter> logger) : ITraewellingRateLimiter
    {
        private readonly ILogger<TraewellingRateLimiter> _logger = logger;
        private readonly object _lock = new();
        private int? _limit;
        private int? _remaining;
        private DateTimeOffset _blockedUntil = DateTimeOffset.MinValue;

        public bool IsLimited
        {
            get { lock (_lock) return _blockedUntil > DateTimeOffset.UtcNow; }
        }

        public async Task WaitIfLimitedAsync(CancellationToken cancellationToken = default)
        {
            TimeSpan wait;
            lock (_lock)
            {
                wait = _blockedUntil - DateTimeOffset.UtcNow;
            }
            if (wait > TimeSpan.Zero)
            {
                _logger.LogInformation("Waiting {Seconds:F0}s for the Träwelling rate limit window to reset", wait.TotalSeconds);
                await Task.Delay(wait, cancellationToken);
            }
        }

        public void RecordResponse(HttpResponseMessage response)
        {
            var limit = ParseHeader(response, "x-ratelimit-limit");
            var remaining = ParseHeader(response, "x-ratelimit-remaining");

            var blockFor = TimeSpan.Zero;
            if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
            {
                blockFor = GetRetryAfter(response) ?? TimeSpan.FromSeconds(60);
            }
            else if (remaining == 0)
            {
                // Laravel's throttle window resets per minute; success responses don't expose
                // the exact reset moment, so waiting one full window is the safe bound.
                blockFor = TimeSpan.FromSeconds(60);
            }

            lock (_lock)
            {
                if (limit.HasValue) _limit = limit;
                if (remaining.HasValue) _remaining = remaining;
                if (blockFor > TimeSpan.Zero)
                {
                    var until = DateTimeOffset.UtcNow.Add(blockFor);
                    if (until > _blockedUntil)
                        _blockedUntil = until;
                }
            }

            if (remaining is < 10)
            {
                _logger.LogWarning("Träwelling API rate limit is low: {Remaining}/{Limit} remaining", remaining, _limit);
            }
        }

        private static int? ParseHeader(HttpResponseMessage response, string name)
        {
            if (response.Headers.TryGetValues(name, out var values) && int.TryParse(values.FirstOrDefault(), out var parsed))
                return parsed;
            return null;
        }

        private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
        {
            var retryAfter = response.Headers.RetryAfter;
            if (retryAfter?.Delta is { } delta)
                return delta;
            if (retryAfter?.Date is { } date)
                return date - DateTimeOffset.UtcNow;
            return null;
        }
    }
}
