using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OV_DB.Models;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace OV_DB.Services
{
    /// <summary>
    /// Talking to the Träwelling API: OAuth connect, token lifetime (refresh tokens rotate on use),
    /// the rate-limited HTTP pipeline, and the few plain reads the rest of the app needs.
    /// </summary>
    public interface ITraewellingApiClient
    {
        /// <summary>
        /// Generate OAuth2 authorization URL for user to connect their Träwelling account
        /// </summary>
        /// <param name="userId">OVDB User ID</param>
        /// <param name="state">OAuth state parameter for security</param>
        /// <param name="withLiveSync">Request webhook creation (live sync) during authorization; only honoured when live sync is configured</param>
        /// <returns>Authorization URL</returns>
        string GetAuthorizationUrl(int userId, string state, bool withLiveSync = false);

        /// <summary>
        /// True when a webhook URL is configured (Traewelling:WebhookUrl), i.e. this
        /// environment can offer live sync
        /// </summary>
        bool IsLiveSyncAvailable { get; }

        /// <summary>
        /// Generate and store OAuth2 state for validation
        /// </summary>
        /// <param name="userId">OVDB User ID</param>
        /// <returns>Generated state parameter</returns>
        string GenerateAndStoreState(int userId);

        /// <summary>
        /// Validate OAuth2 state parameter
        /// </summary>
        /// <param name="state">State parameter from callback</param>
        /// <param name="userId">OVDB User ID</param>
        /// <returns>True if state is valid</returns>
        bool ValidateAndConsumeState(string state,out int? userId);

        /// <summary>
        /// Exchange OAuth2 authorization code for access tokens and store with user
        /// </summary>
        /// <param name="code">Authorization code from OAuth callback</param>
        /// <param name="state">State parameter for validation</param>
        /// <param name="userId">OVDB User ID</param>
        /// <returns>Success status</returns>
        Task<bool> ExchangeCodeForTokensAsync(string code, string state, int userId);

        /// <summary>
        /// Get authenticated user information from Träwelling
        /// </summary>
        /// <param name="user">User with valid tokens</param>
        /// <returns>Träwelling user data</returns>
        Task<TrawellingUserAuthData> GetUserInfoAsync(User user);

        /// <summary>
        /// Check whether the user's stored webhook still exists and is enabled upstream
        /// </summary>
        /// <param name="user">User to check</param>
        /// <returns>Health of the webhook subscription</returns>
        Task<TrawellingWebhookHealth> GetWebhookHealthAsync(User user);

        /// <summary>
        /// Delete the user's webhook upstream (best effort) and clear the stored webhook data
        /// </summary>
        /// <param name="user">User to remove the webhook for</param>
        Task RemoveWebhookAsync(User user);

        /// <summary>
        /// Check if user has a usable Träwelling connection: either a currently valid
        /// access token or a refresh token that can be used to obtain one
        /// </summary>
        /// <param name="user">User to check</param>
        /// <returns>True if the connection can still be used</returns>
        bool IsConnected(User user);

        /// <summary>
        /// Ensure the user has a valid access token, refreshing it if necessary.
        /// Refreshes are serialized per user to avoid revoking a concurrently rotated refresh token.
        /// </summary>
        /// <param name="user">User to ensure tokens for</param>
        /// <returns>True if a valid access token is available</returns>
        Task<bool> EnsureValidTokenAsync(User user);

        /// <summary>
        /// Fetch active warning/danger alerts from the Träwelling platform
        /// </summary>
        /// <param name="user">User with valid tokens</param>
        /// <returns>List of active warning or danger alerts</returns>
        Task<List<TrawellingAlert>> GetAlertsAsync(User user);

        /// <summary>
        /// The full calling pattern of a trip — every station the train stops at, not just the two
        /// the check-in covers.
        /// </summary>
        /// <remarks>
        /// Costs one API call, so it is made <b>only when a trip is actually being imported</b>,
        /// never while listing or browsing check-ins. This is the one signal that can tell stopping
        /// apart from passing through, which route geometry cannot.
        /// </remarks>
        /// <param name="user">User with valid tokens</param>
        /// <param name="tripId">Träwelling trip id, from <c>status.Checkin.Trip</c></param>
        Task<List<TrawellingStopover>> GetTripStopoversAsync(User user, int tripId, CancellationToken cancellationToken = default);

        /// <summary>
        /// The full status, both as the raw <c>data</c> JSON (kept as the inbox payload) and
        /// parsed. (null, null) when there is no valid token or the call fails.
        /// </summary>
        /// <param name="waitIfLimited">False for callers that must answer quickly (webhook deliveries):
        /// gives up instead of waiting out the rate limit or retrying.</param>
        Task<(string Json, TrawellingStatus Status)> FetchStatusRawAsync(User user, int statusId, bool waitIfLimited = true);

        /// <summary>
        /// GET an API path (relative to Traewelling:BaseUrl) as this user, through the shared
        /// rate limiter. The caller checks <see cref="EnsureValidTokenAsync"/> first.
        /// </summary>
        Task<HttpResponseMessage> GetAsync(User user, string path, CancellationToken cancellationToken = default);
    }

    public class TraewellingApiClient : ITraewellingApiClient
    {
        public const string HTTP_CLIENT_NAME = "TrawellingServiceClient";
        private readonly HttpClient _httpClient;
        private readonly OVDBDatabaseContext _dbContext;
        private readonly ILogger<TraewellingApiClient> _logger;
        private readonly ITraewellingRateLimiter _rateLimiter;
        private readonly string _baseUrl;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _redirectUri;
        private readonly string _authorizeUrl;
        private readonly string _tokenUrl;
        private readonly string _webhookUrl;

        public TraewellingApiClient(IHttpClientFactory httpClientFactory, IConfiguration configuration,
            OVDBDatabaseContext dbContext, ILogger<TraewellingApiClient> logger, ITraewellingRateLimiter rateLimiter)
        {
            _httpClient = httpClientFactory.CreateClient(HTTP_CLIENT_NAME);
            _dbContext = dbContext;
            _logger = logger;
            _rateLimiter = rateLimiter;
            _baseUrl = configuration["Traewelling:BaseUrl"];
            _clientId = configuration["Traewelling:ClientId"];
            _clientSecret = configuration["Traewelling:ClientSecret"];
            _redirectUri = configuration["Traewelling:RedirectUri"];
            _authorizeUrl = configuration["Traewelling:AuthorizeUrl"];
            _tokenUrl = configuration["Traewelling:TokenUrl"];
            // Live sync (webhooks) is only available when this exactly matches the
            // authorized_webhook_url registered on the OAuth client — production only
            _webhookUrl = configuration["Traewelling:WebhookUrl"];
        }

        // Simple in-memory cache for OAuth states - in production, use Redis or database
        private static readonly Dictionary<string, (int UserId, DateTime Expiry)> _oauthStates = new();

        private static readonly object _statelock = new object();

        public bool IsLiveSyncAvailable => !string.IsNullOrEmpty(_webhookUrl);

        public string GetAuthorizationUrl(int userId, string state, bool withLiveSync = false)
        {
            var queryParams = HttpUtility.ParseQueryString(string.Empty);
            queryParams["response_type"] = "code";
            queryParams["client_id"] = _clientId;
            queryParams["redirect_uri"] = _redirectUri;
            queryParams["scope"] = "read-statuses";
            queryParams["state"] = state;

            if (withLiveSync && IsLiveSyncAvailable)
            {
                // Asks Träwelling to create a webhook during authorization; the URL must
                // exactly equal the authorized_webhook_url stored on the OAuth client
                queryParams["trwl_webhook_url"] = _webhookUrl;
                queryParams["trwl_webhook_events"] = "checkin_create,checkin_update,checkin_delete";
            }

            return $"{_authorizeUrl}?{queryParams}";
        }

        public string GenerateAndStoreState(int userId)
        {
            var state = Guid.NewGuid().ToString();
            var expiry = DateTime.UtcNow.AddMinutes(10); // States expire in 10 minutes

            lock (_statelock)
            {
                // Clean up expired states
                var expiredStates = _oauthStates.Where(kvp => kvp.Value.Expiry < DateTime.UtcNow).ToList();
                foreach (var expired in expiredStates)
                {
                    _oauthStates.Remove(expired.Key);
                }

                // Store new state
                _oauthStates[state] = (userId, expiry);
                _logger.LogDebug("Generated OAuth state {State} for user {UserId}", state, userId);
            }

            return state;
        }

        public bool ValidateAndConsumeState(string state, out int? userId)
        {
            userId = null;
            if (string.IsNullOrEmpty(state))
                return false;

            lock (_statelock)
            {
                if (_oauthStates.TryGetValue(state, out var stateInfo))
                {
                    // Remove the state (one-time use)
                    _oauthStates.Remove(state);

                    // Check if expired
                    if (stateInfo.Expiry < DateTime.UtcNow)
                    {
                        _logger.LogWarning("OAuth state {State} has expired", state);
                        return false;
                    }

                    userId = stateInfo.UserId;

                    _logger.LogDebug("OAuth state {State} validated for user {UserId}", state, userId);
                    return true;
                }

                _logger.LogWarning("OAuth state {State} not found", state);
                return false;
            }
        }

        public async Task<bool> ExchangeCodeForTokensAsync(string code, string state, int userId)
        {
            try
            {
                var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Post, _tokenUrl)
                {
                    Content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("grant_type", "authorization_code"),
                        new KeyValuePair<string, string>("client_id", _clientId),
                        new KeyValuePair<string, string>("client_secret", _clientSecret),
                        new KeyValuePair<string, string>("redirect_uri", _redirectUri),
                        new KeyValuePair<string, string>("code", code)
                    })
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to exchange code for tokens. Status: {StatusCode}, Content: {Content}",
                        response.StatusCode, await response.Content.ReadAsStringAsync());
                    return false;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonConvert.DeserializeObject<TrawellingTokenResponse>(responseContent);

                var user = await _dbContext.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogError("User with ID {UserId} not found", userId);
                    return false;
                }

                user.TrawellingAccessToken = tokenResponse.AccessToken;
                user.TrawellingRefreshToken = tokenResponse.RefreshToken;
                user.TrawellingTokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

                if (tokenResponse.Webhook != null)
                {
                    // The exchange response is the only moment the webhook secret is issued
                    user.TraewellingWebhookId = tokenResponse.Webhook.Id;
                    user.TraewellingWebhookSecret = tokenResponse.Webhook.Secret;
                    user.TraewellingWebhookCreatedAt = DateTime.UtcNow;
                    _logger.LogInformation("Stored Träwelling webhook {WebhookId} for user {UserId}", tokenResponse.Webhook.Id, userId);
                }

                // Fetch and store user information including username
                var userInfo = await GetUserInfoAsync(user);
                if (userInfo != null)
                {
                    user.TrawellingUsername = userInfo.Username;
                    _logger.LogInformation("Stored Träwelling username {Username} for user {UserId}", userInfo.Username, userId);
                }
                else
                {
                    _logger.LogWarning("Failed to fetch Träwelling user info for user {UserId}", userId);
                }

                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Successfully stored Träwelling tokens for user {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exchanging code for tokens for user {UserId}", userId);
                return false;
            }
        }

        internal static bool IsInvalidGrant(System.Net.HttpStatusCode status, string body)
        {
            if (status != System.Net.HttpStatusCode.BadRequest || string.IsNullOrWhiteSpace(body))
                return false;
            try
            {
                return Newtonsoft.Json.Linq.JObject.Parse(body).Value<string>("error") == "invalid_grant";
            }
            catch (JsonException)
            {
                return false;
            }
        }

        internal async Task<bool> RefreshTokensAsync(User user)
        {
            try
            {
                if (string.IsNullOrEmpty(user.TrawellingRefreshToken))
                {
                    _logger.LogWarning("No refresh token available for user {UserId}", user.Id);
                    return false;
                }

                var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Post, _tokenUrl)
                {
                    Content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("grant_type", "refresh_token"),
                        new KeyValuePair<string, string>("client_id", _clientId),
                        new KeyValuePair<string, string>("client_secret", _clientSecret),
                        new KeyValuePair<string, string>("refresh_token", user.TrawellingRefreshToken)
                    })
                });

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to refresh tokens for user {UserId}. Status: {StatusCode}, Content: {Content}",
                        user.Id, response.StatusCode, errorBody);

                    // Only invalid_grant means this user's refresh token was rejected (expired or
                    // revoked); clear it so the connection status honestly reports disconnected.
                    // Anything else (invalid_client after a mistyped or rotated client secret, an
                    // outage) is not the token's fault, and clearing would disconnect every user.
                    if (IsInvalidGrant(response.StatusCode, errorBody))
                    {
                        user.TrawellingAccessToken = null;
                        user.TrawellingRefreshToken = null;
                        user.TrawellingTokenExpiresAt = null;
                        await _dbContext.SaveChangesAsync();
                    }
                    return false;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonConvert.DeserializeObject<TrawellingTokenResponse>(responseContent);

                user.TrawellingAccessToken = tokenResponse.AccessToken;
                user.TrawellingRefreshToken = tokenResponse.RefreshToken;
                user.TrawellingTokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

                // Persist the rotated tokens IMMEDIATELY, before any further network call. Träwelling
                // invalidates the old refresh token the moment this response is issued; if we threw or
                // crashed before saving, the DB would keep the dead token and the connection would break.
                await _dbContext.SaveChangesAsync();

                // Fetch and store user information including username if not already stored
                if (string.IsNullOrEmpty(user.TrawellingUsername))
                {
                    var userInfo = await GetUserInfoAsync(user);
                    if (userInfo != null)
                    {
                        user.TrawellingUsername = userInfo.Username;
                        _logger.LogInformation("Stored Träwelling username {Username} for user {UserId}", userInfo.Username, user.Id);
                        await _dbContext.SaveChangesAsync();
                    }
                }

                _logger.LogInformation("Successfully refreshed tokens for user {UserId}", user.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing tokens for user {UserId}", user.Id);
                return false;
            }
        }

        public async Task<TrawellingUserAuthData> GetUserInfoAsync(User user)
        {
            try
            {
                if (!await EnsureValidTokenAsync(user))
                    return null;

                var response = await SendAsync(() => CreateApiRequest(HttpMethod.Get, $"{_baseUrl}/auth/user", user));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get user info for user {UserId}. Status: {StatusCode}",
                        user.Id, response.StatusCode);
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var userResponse = JsonConvert.DeserializeObject<TrawellingUserAuthResponse>(responseContent);

                return userResponse?.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user info for user {UserId}", user.Id);
                return null;
            }
        }

        public async Task<TrawellingWebhookHealth> GetWebhookHealthAsync(User user)
        {
            if (!user.TraewellingWebhookId.HasValue)
                return TrawellingWebhookHealth.NotEnabled;

            try
            {
                if (!await EnsureValidTokenAsync(user))
                    return TrawellingWebhookHealth.Unknown;

                var response = await SendAsync(() => CreateApiRequest(HttpMethod.Get, $"{_baseUrl}/webhooks", user));
                if (!response.IsSuccessStatusCode)
                    return TrawellingWebhookHealth.Unknown;

                var content = await response.Content.ReadAsStringAsync();
                var webhooks = JsonConvert.DeserializeObject<TrawellingWebhooksResponse>(content);
                var webhook = webhooks?.Data?.FirstOrDefault(w => w.Id == user.TraewellingWebhookId.Value);

                if (webhook == null)
                    return TrawellingWebhookHealth.Missing;
                return webhook.DisabledAt.HasValue
                    ? TrawellingWebhookHealth.DisabledUpstream
                    : TrawellingWebhookHealth.Active;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Träwelling webhook health for user {UserId}", user.Id);
                return TrawellingWebhookHealth.Unknown;
            }
        }

        public async Task RemoveWebhookAsync(User user)
        {
            if (user.TraewellingWebhookId.HasValue)
            {
                // Best effort: deleting the upstream webhook needs a valid token, which may
                // already be gone; the local columns are cleared regardless.
                try
                {
                    if (await EnsureValidTokenAsync(user))
                    {
                        var response = await SendAsync(() =>
                            CreateApiRequest(HttpMethod.Delete, $"{_baseUrl}/webhooks/{user.TraewellingWebhookId.Value}", user));
                        _logger.LogInformation("Deleted Träwelling webhook {WebhookId} for user {UserId}: {StatusCode}",
                            user.TraewellingWebhookId.Value, user.Id, (int)response.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not delete Träwelling webhook {WebhookId} for user {UserId}", user.TraewellingWebhookId, user.Id);
                }
            }

            user.TraewellingWebhookId = null;
            user.TraewellingWebhookSecret = null;
            user.TraewellingWebhookCreatedAt = null;
            await _dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// Fetch a single status from the API as raw JSON plus its parsed form. Unlike the
        /// webhook delivery payload, this endpoint returns the complete resource including
        /// the stopovers' station objects.
        /// </summary>
        public async Task<(string Json, TrawellingStatus Status)> FetchStatusRawAsync(User user, int statusId, bool waitIfLimited = true)
        {
            try
            {
                if (!waitIfLimited && _rateLimiter.IsLimited)
                    return (null, null);
                if (!await EnsureValidTokenAsync(user))
                    return (null, null);

                var response = await SendAsync(() =>
                    CreateApiRequest(HttpMethod.Get, $"{_baseUrl}/status/{statusId}", user), maxRetries: waitIfLimited ? 5 : 0);
                if (!response.IsSuccessStatusCode)
                    return (null, null);

                var content = await response.Content.ReadAsStringAsync();
                if (JObject.Parse(content)["data"] is not JObject data)
                    return (null, null);

                return (data.ToString(Formatting.None), data.ToObject<TrawellingStatus>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching full status {StatusId} for user {UserId}", statusId, user.Id);
                return (null, null);
            }
        }

        public bool HasValidTokens(User user)
        {
            return !string.IsNullOrEmpty(user.TrawellingAccessToken) &&
                   user.TrawellingTokenExpiresAt.HasValue &&
                   user.TrawellingTokenExpiresAt.Value > DateTime.UtcNow.AddMinutes(5);
        }

        public bool IsConnected(User user)
        {
            // The connection is alive as long as we hold a refresh token, even when the
            // short-lived access token has expired — it can be refreshed on demand
            return HasValidTokens(user) || !string.IsNullOrEmpty(user.TrawellingRefreshToken);
        }

        // Träwelling rotates refresh tokens on every use; a concurrent double-refresh would
        // revoke the token the other request just received, so refreshes are serialized per user
        private static readonly ConcurrentDictionary<int, SemaphoreSlim> _refreshLocks = new();

        public async Task<bool> EnsureValidTokenAsync(User user)
        {
            if (HasValidTokens(user))
                return true;

            if (string.IsNullOrEmpty(user.TrawellingRefreshToken))
            {
                _logger.LogWarning("User {UserId} has no valid Träwelling tokens", user.Id);
                return false;
            }

            var refreshLock = _refreshLocks.GetOrAdd(user.Id, _ => new SemaphoreSlim(1, 1));
            await refreshLock.WaitAsync();
            try
            {
                // A parallel request may have refreshed while we waited; pick up its tokens
                await _dbContext.Entry(user).ReloadAsync();
                if (HasValidTokens(user))
                    return true;

                if (string.IsNullOrEmpty(user.TrawellingRefreshToken))
                    return false;

                return await RefreshTokensAsync(user);
            }
            finally
            {
                refreshLock.Release();
            }
        }

        public async Task<List<TrawellingAlert>> GetAlertsAsync(User user)
        {
            try
            {
                if (!await EnsureValidTokenAsync(user))
                    return new List<TrawellingAlert>();

                var response = await SendAsync(() => CreateApiRequest(HttpMethod.Get, $"{_baseUrl}/alerts", user));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get Träwelling alerts. Status: {StatusCode}", response.StatusCode);
                    return new List<TrawellingAlert>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var alertsResponse = JsonConvert.DeserializeObject<TrawellingApiAlertsResponse>(content);

                return alertsResponse?.Data
                    ?.Where(a => a.Type == "warning" || a.Type == "danger")
                    .ToList() ?? new List<TrawellingAlert>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Träwelling alerts");
                return new List<TrawellingAlert>();
            }
        }

        /// <summary>A bearer-authenticated request for this user; built per attempt by <see cref="SendAsync"/>.</summary>
        private static HttpRequestMessage CreateApiRequest(HttpMethod method, string url, User user)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", user.TrawellingAccessToken);
            return request;
        }

        /// <summary>
        /// Every station this trip calls at. See <see cref="ITraewellingApiClient.GetTripStopoversAsync"/>
        /// for why this is only ever called at import.
        /// </summary>
        public async Task<List<TrawellingStopover>> GetTripStopoversAsync(User user, int tripId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!await EnsureValidTokenAsync(user))
                    return [];

                var response = await SendAsync(
                    () => CreateApiRequest(HttpMethod.Get, $"{_baseUrl}/stopovers/{tripId}", user),
                    cancellationToken: cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Träwelling returned {StatusCode} fetching stopovers for trip {TripId}",
                        (int)response.StatusCode, tripId);
                    return [];
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                if (JObject.Parse(content)["data"] is not JObject data)
                    return [];

                // Keyed by trip id, because the endpoint accepts a comma-separated list. We only
                // ever ask for one, so take whichever key came back rather than assuming its shape.
                var stopovers = data[tripId.ToString()] ?? data.Properties().FirstOrDefault()?.Value;
                return stopovers?.ToObject<List<TrawellingStopover>>() ?? [];
            }
            catch (OperationCanceledException)
            {
                // The caller gave up waiting, which is a decision rather than a fault. Passed on
                // so it can say the pattern went unread instead of being told there was none.
                throw;
            }
            catch (Exception ex)
            {
                // A missing calling pattern costs suggestions, not the import. Never fail the one
                // for the other.
                _logger.LogError(ex, "Error fetching stopovers for trip {TripId} for user {UserId}", tripId, user.Id);
                return [];
            }
        }

        /// <summary>
        /// Central pipeline for every Träwelling HTTP call: waits for the shared rate-limit
        /// window when exhausted, builds a fresh request per attempt (auth header included,
        /// no shared-client header mutation), records rate-limit headers from every response
        /// and retries on 429/503.
        /// </summary>
        private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> requestFactory, int maxRetries = 5, CancellationToken cancellationToken = default)
        {
            int attempt = 0;
            while (true)
            {
                await _rateLimiter.WaitIfLimitedAsync(cancellationToken);
                using var request = requestFactory();
                var response = await _httpClient.SendAsync(request, cancellationToken);
                _rateLimiter.RecordResponse(response);

                if (response.StatusCode != System.Net.HttpStatusCode.TooManyRequests &&
                    response.StatusCode != System.Net.HttpStatusCode.ServiceUnavailable)
                    return response;

                if (attempt >= maxRetries)
                {
                    _logger.LogWarning("Träwelling API rate limited after {MaxRetries} retries, giving up.", maxRetries);
                    return response;
                }

                attempt++;
                _logger.LogWarning("Träwelling API returned {StatusCode}. Retrying (attempt {Attempt}/{MaxRetries}).",
                    (int)response.StatusCode, attempt, maxRetries);
                response.Dispose();

                // The limiter recorded the Retry-After, so WaitIfLimitedAsync at the top of the
                // loop does the waiting; fall back to exponential backoff if there was none.
                if (!_rateLimiter.IsLimited)
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
            }
        }

        public Task<HttpResponseMessage> GetAsync(User user, string path, CancellationToken cancellationToken = default) =>
            SendAsync(() => CreateApiRequest(HttpMethod.Get, $"{_baseUrl}/{path}", user), cancellationToken: cancellationToken);
    }
}
