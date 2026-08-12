using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OV_DB.Services
{
    public class OverpassEndpoint
    {
        public string Url { get; set; }
        public bool SupportsAttic { get; set; }
    }

    public class OverpassService : IOverpassService
    {
        // Ordered by priority. Overridable via the "Overpass:Endpoints" config section.
        private static readonly List<OverpassEndpoint> DefaultEndpoints =
        [
            new OverpassEndpoint { Url = "https://overpass.kumi.systems/api/interpreter", SupportsAttic = true },
            new OverpassEndpoint { Url = "https://overpass-api.de/api/interpreter", SupportsAttic = true },
            new OverpassEndpoint { Url = "https://overpass.openstreetmap.fr/api/interpreter", SupportsAttic = false },
            new OverpassEndpoint { Url = "https://overpass.private.coffee/api/interpreter", SupportsAttic = false },
        ];

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OverpassService> _logger;
        private readonly List<OverpassEndpoint> _endpoints;
        private readonly TimeSpan _failureCooldown;
        private readonly ConcurrentDictionary<string, DateTimeOffset> _cooldownUntil = new();
        private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _inFlight = new();
        private readonly SemaphoreSlim _throttle;

        public OverpassService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<OverpassService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            var configured = configuration.GetSection("Overpass:Endpoints").Get<List<OverpassEndpoint>>();
            _endpoints = configured is { Count: > 0 } ? configured : DefaultEndpoints;
            _failureCooldown = TimeSpan.FromSeconds(configuration.GetValue("Overpass:FailureCooldownSeconds", 120));
            // Public Overpass instances typically allow ~2 request slots per IP.
            _throttle = new SemaphoreSlim(configuration.GetValue("Overpass:MaxConcurrentRequests", 2));
        }

        public Task<string> QueryAsync(string query, CancellationToken cancellationToken = default)
        {
            // Coalesce identical concurrent queries into a single upstream request; the shared
            // request itself is not tied to any one caller's token, WaitAsync detaches each
            // caller on its own cancellation instead.
            var inFlight = _inFlight.GetOrAdd(query, q => new Lazy<Task<string>>(() => RunQueryAsync(q)));
            return inFlight.Value.WaitAsync(cancellationToken);
        }

        private async Task<string> RunQueryAsync(string query)
        {
            try
            {
                await _throttle.WaitAsync();
                try
                {
                    return await ExecuteWithFailoverAsync(query);
                }
                finally
                {
                    _throttle.Release();
                }
            }
            finally
            {
                _inFlight.TryRemove(query, out _);
            }
        }

        private async Task<string> ExecuteWithFailoverAsync(string query)
        {
            var eligible = RequiresAttic(query)
                ? _endpoints.Where(e => e.SupportsAttic).ToList()
                : _endpoints;
            if (eligible.Count == 0)
            {
                _logger.LogError("No Overpass endpoint supports attic data, cannot run historic query");
                return null;
            }

            // Prefer endpoints that are not cooling down after a recent failure, but if
            // everything is cooling down just try them all anyway rather than give up.
            var candidates = eligible.Where(e => !IsCoolingDown(e.Url)).ToList();
            if (candidates.Count == 0)
                candidates = eligible;

            var httpClient = _httpClientFactory.CreateClient("OSM");
            foreach (var endpoint in candidates)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    using var content = new StringContent(query);
                    var response = await httpClient.PostAsync(endpoint.Url, content);
                    if (response.IsSuccessStatusCode)
                    {
                        _cooldownUntil.TryRemove(endpoint.Url, out _);
                        var body = await response.Content.ReadAsStringAsync();
                        _logger.LogInformation("Overpass query answered by {Url} in {ElapsedMs} ms ({Length} chars)", endpoint.Url, stopwatch.ElapsedMilliseconds, body.Length);
                        return body;
                    }
                    if (IsRetryable(response.StatusCode))
                    {
                        StartCooldown(endpoint.Url, GetRetryAfter(response));
                        _logger.LogWarning("Overpass endpoint {Url} returned {StatusCode}, trying next endpoint", endpoint.Url, (int)response.StatusCode);
                        continue;
                    }
                    // A non-retryable client error (e.g. 400 for a malformed query) will fail
                    // identically on every mirror, so don't bother failing over.
                    _logger.LogWarning("Overpass endpoint {Url} rejected query with {StatusCode}", endpoint.Url, (int)response.StatusCode);
                    return null;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    // TaskCanceledException here is the HttpClient timeout; caller cancellation
                    // never reaches this task (see QueryAsync).
                    StartCooldown(endpoint.Url);
                    _logger.LogWarning(ex, "Overpass endpoint {Url} failed, trying next endpoint", endpoint.Url);
                }
            }
            _logger.LogError("All eligible Overpass endpoints failed for query");
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

        private static bool RequiresAttic(string query)
        {
            // Overpass QL: [date:"..."]; XML: <osm-script output="json" date="...">
            return query.Contains("[date:", StringComparison.OrdinalIgnoreCase)
                || (query.Contains("<osm-script", StringComparison.OrdinalIgnoreCase) && query.Contains(" date=\"", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsRetryable(HttpStatusCode statusCode) =>
            statusCode == HttpStatusCode.TooManyRequests
            || statusCode == HttpStatusCode.RequestTimeout
            || (int)statusCode >= 500;

        private bool IsCoolingDown(string url) =>
            _cooldownUntil.TryGetValue(url, out var until) && until > DateTimeOffset.UtcNow;

        private void StartCooldown(string url, TimeSpan? retryAfter = null)
        {
            var cooldown = retryAfter > TimeSpan.Zero ? retryAfter.Value : _failureCooldown;
            _cooldownUntil[url] = DateTimeOffset.UtcNow.Add(cooldown);
        }
    }
}
