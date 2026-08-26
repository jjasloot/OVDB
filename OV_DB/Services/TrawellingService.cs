using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using OV_DB.Hubs;
using OV_DB.Models;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace OV_DB.Services
{
    public class TrawellingService : ITrawellingService
    {
        public static string HTTP_CLIENT_NAME = "TrawellingServiceClient";
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ITimezoneService _timezoneService;
        private readonly OVDBDatabaseContext _dbContext;
        private readonly ILogger<TrawellingService> _logger;
        private readonly string _baseUrl;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _redirectUri;
        private readonly string _authorizeUrl;
        private readonly string _tokenUrl;
        private readonly string _webhookUrl;
        private readonly IMemoryCache _memoryCache;
        private readonly ITraewellingRateLimiter _rateLimiter;
        private readonly IHubContext<TraewellingHub> _traewellingHubContext;

        // Simple in-memory cache for OAuth states - in production, use Redis or database
        private static readonly Dictionary<string, (int UserId, DateTime Expiry)> _oauthStates = new();
        private static readonly object _statelock = new object();

        // Matches the REST API's Newtonsoft configuration so hub payloads have the exact
        // same shape (camelCase, string enums via the DTO attributes) as the list endpoint
        private static readonly JsonSerializerSettings ApiJsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        };

        public TrawellingService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ITimezoneService timezoneService,
            OVDBDatabaseContext dbContext, ILogger<TrawellingService> logger, IMemoryCache memoryCache, ITraewellingRateLimiter rateLimiter,
            IHubContext<TraewellingHub> traewellingHubContext)
        {
            _httpClient = httpClientFactory.CreateClient(HTTP_CLIENT_NAME);
            _configuration = configuration;
            _timezoneService = timezoneService;
            _dbContext = dbContext;
            _logger = logger;
            _memoryCache = memoryCache;
            _rateLimiter = rateLimiter;
            _traewellingHubContext = traewellingHubContext;
            _baseUrl = _configuration["Traewelling:BaseUrl"];
            _clientId = _configuration["Traewelling:ClientId"];
            _clientSecret = _configuration["Traewelling:ClientSecret"];
            _redirectUri = _configuration["Traewelling:RedirectUri"];
            _authorizeUrl = _configuration["Traewelling:AuthorizeUrl"];
            _tokenUrl = _configuration["Traewelling:TokenUrl"];
            // Live sync (webhooks) is only available when this exactly matches the
            // authorized_webhook_url registered on the OAuth client — production only
            _webhookUrl = _configuration["Traewelling:WebhookUrl"];
        }

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

        public async Task<bool> RefreshTokensAsync(User user)
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
                    _logger.LogError("Failed to refresh tokens for user {UserId}. Status: {StatusCode}, Content: {Content}",
                        user.Id, response.StatusCode, await response.Content.ReadAsStringAsync());

                    // 400/401 means the refresh token itself was rejected (expired or revoked);
                    // clear the tokens so the connection status honestly reports disconnected
                    if (response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
                        response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
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

        private const int SweepMaxPages = 5;
        private static readonly TimeSpan SweepStaleness = TimeSpan.FromHours(1);

        public async Task<bool> SweepInboxAsync(User user, bool force = false, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!force && user.TrawellingLastSweepAt.HasValue &&
                    DateTime.UtcNow - user.TrawellingLastSweepAt.Value < SweepStaleness)
                {
                    return true;
                }

                if (!await EnsureValidTokenAsync(user))
                    return false;

                // Ensure we have the username for this user
                if (string.IsNullOrEmpty(user.TrawellingUsername))
                {
                    var userInfo = await GetUserInfoAsync(user);
                    if (userInfo == null)
                    {
                        _logger.LogError("Could not fetch Träwelling username for user {UserId}", user.Id);
                        return false;
                    }
                    user.TrawellingUsername = userInfo.Username;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Fetched and stored Träwelling username {Username} for user {UserId}", userInfo.Username, user.Id);
                }

                // Everything already known for this user — scoped to THIS user, otherwise two
                // accounts linked to the same Träwelling account hide each other's trips.
                var knownIds = new HashSet<int>(await _dbContext.RouteInstances
                    .Where(ri => ri.TrawellingStatusId.HasValue)
                    .Where(ri => ri.RouteInstanceMaps.Any(rim => rim.Map.UserId == user.Id)
                        || ri.Route.RouteMaps.Any(rm => rm.Map.UserId == user.Id))
                    .Select(ri => ri.TrawellingStatusId.Value)
                    .ToListAsync(cancellationToken));
                knownIds.UnionWith(await _dbContext.TrawellingIgnoredStatuses
                    .Where(tis => tis.UserId == user.Id)
                    .Select(tis => tis.TrawellingStatusId)
                    .ToListAsync(cancellationToken));
                knownIds.UnionWith(await _dbContext.TrawellingInboxStatuses
                    .Where(s => s.UserId == user.Id)
                    .Select(s => s.TrawellingStatusId)
                    .ToListAsync(cancellationToken));

                var seenIds = new HashSet<int>();
                DateTime? oldestSweptDeparture = null;
                var added = 0;

                for (var page = 1; page <= SweepMaxPages; page++)
                {
                    var response = await SendAsync(() =>
                        CreateApiRequest(HttpMethod.Get, $"{_baseUrl}/user/{user.TrawellingUsername}/statuses?page={page}", user),
                        cancellationToken: cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("Inbox sweep failed for user {UserId}. Status: {StatusCode}", user.Id, response.StatusCode);
                        return false;
                    }

                    var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    var statusesResponse = JsonConvert.DeserializeObject<TrawellingStatusesResponse>(responseContent);
                    if (statusesResponse?.Data == null || statusesResponse.Data.Count == 0)
                        break;

                    var newOnPage = 0;
                    foreach (var status in statusesResponse.Data)
                    {
                        seenIds.Add(status.Id);
                        var departure = GetStatusDeparture(status);
                        if (departure.HasValue && (!oldestSweptDeparture.HasValue || departure < oldestSweptDeparture))
                            oldestSweptDeparture = departure;

                        if (knownIds.Contains(status.Id))
                            continue;

                        _dbContext.TrawellingInboxStatuses.Add(new TrawellingInboxStatus
                        {
                            UserId = user.Id,
                            TrawellingStatusId = status.Id,
                            PayloadJson = JsonConvert.SerializeObject(status),
                            State = TrawellingInboxState.Pending,
                            Source = TrawellingInboxSource.Sweep,
                            DepartureAt = departure,
                            ReceivedAt = DateTime.UtcNow,
                            LastEventAt = DateTime.UtcNow,
                        });
                        knownIds.Add(status.Id);
                        newOnPage++;
                        added++;
                    }

                    // Statuses are ordered newest-first: a full page without anything new means
                    // everything older is already known too.
                    if (newOnPage == 0 || string.IsNullOrEmpty(statusesResponse.Links?.Next))
                        break;
                }

                // Heal upstream deletes: a pending row inside the swept departure range that no
                // longer appears in the listing was deleted on Träwelling. Nothing was curated
                // yet, so it can simply be dropped.
                if (seenIds.Count > 0 && oldestSweptDeparture.HasValue)
                {
                    var deletedUpstream = await _dbContext.TrawellingInboxStatuses
                        .Where(s => s.UserId == user.Id
                            && s.State == TrawellingInboxState.Pending
                            && s.DepartureAt >= oldestSweptDeparture.Value
                            && !seenIds.Contains(s.TrawellingStatusId))
                        .ToListAsync(cancellationToken);
                    if (deletedUpstream.Count > 0)
                    {
                        _dbContext.RemoveRange(deletedUpstream);
                        _logger.LogInformation("Inbox sweep removed {Count} upstream-deleted statuses for user {UserId}", deletedUpstream.Count, user.Id);
                    }
                }

                user.TrawellingLastSweepAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);

                if (added > 0)
                    _logger.LogInformation("Inbox sweep added {Added} statuses for user {UserId}", added, user.Id);
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sweeping Träwelling inbox for user {UserId}", user.Id);
                return false;
            }
        }

        private static DateTime? GetStatusDeparture(TrawellingStatus status)
        {
            var departure = status.Checkin?.ManualDeparture
                ?? status.Checkin?.Origin?.DepartureReal
                ?? status.Checkin?.Origin?.DeparturePlanned;
            return departure?.UtcDateTime;
        }

        public async Task<TrawellingTripsResponse> GetOptimizedTripsAsync(User user, int page = 1, bool refresh = false, CancellationToken cancellationToken = default)
        {
            try
            {
                // The inbox is the source of the list; the statuses API is only touched by the
                // sweep (skipped when fresh, forced by the frontend's refresh action). A failed
                // sweep still returns the current — possibly stale — inbox contents.
                await SweepInboxAsync(user, force: refresh, cancellationToken);

                const int pageSize = 15;
                var query = _dbContext.TrawellingInboxStatuses
                    .Where(s => s.UserId == user.Id && s.State == TrawellingInboxState.Pending)
                    .OrderByDescending(s => s.DepartureAt)
                    .ThenByDescending(s => s.TrawellingStatusId);

                var totalCount = await query.CountAsync();
                var rows = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var optimizedTrips = new List<TrawellingTripDto>();
                foreach (var row in rows)
                {
                    // Check if we have timezone-corrected trip cached
                    var cacheKey = $"TrawellingTrip|{row.TrawellingStatusId}";
                    if (_memoryCache.TryGetValue(cacheKey, out TrawellingTripDto cachedTrip))
                    {
                        optimizedTrips.Add(cachedTrip);
                        continue;
                    }

                    TrawellingStatus status = null;
                    try
                    {
                        status = JsonConvert.DeserializeObject<TrawellingStatus>(row.PayloadJson);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Could not parse inbox payload for status {StatusId}, user {UserId}", row.TrawellingStatusId, user.Id);
                    }

                    var trip = status != null ? await MapStatusToTripDtoAsync(user, status) : null;
                    if (trip == null)
                    {
                        // Early webhook deliveries were stored without the stopovers' station
                        // objects, which the mapping requires; refetch the complete status once
                        // and repair the stored payload.
                        var (fullJson, fullStatus) = await FetchStatusRawAsync(user, row.TrawellingStatusId);
                        if (fullJson != null)
                        {
                            row.PayloadJson = fullJson;
                            row.DepartureAt = GetStatusDeparture(fullStatus);
                            row.LastEventAt = DateTime.UtcNow;
                            await _dbContext.SaveChangesAsync();
                            trip = await MapStatusToTripDtoAsync(user, fullStatus);
                        }
                    }

                    if (trip != null)
                    {
                        // Cache the timezone-corrected trip
                        _memoryCache.Set(cacheKey, trip, TimeSpan.FromMinutes(30));
                        optimizedTrips.Add(trip);
                    }
                    else
                    {
                        _logger.LogWarning("Inbox status {StatusId} for user {UserId} could not be mapped to a trip and was skipped", row.TrawellingStatusId, user.Id);
                    }
                }

                var lastPage = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
                return new TrawellingTripsResponse
                {
                    Data = optimizedTrips,
                    Meta = new TrawellingPaginationMeta
                    {
                        CurrentPage = page,
                        Total = totalCount,
                        PerPage = pageSize,
                    },
                    HasMorePages = page < lastPage
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting optimized trips for user {UserId}", user.Id);
                return null;
            }
        }

        /// <summary>
        /// The inbox is a queue, not an archive: once a status is imported or ignored its row
        /// is removed — imported/ignored state lives on RouteInstance.TrawellingStatusId and
        /// TrawellingIgnoredStatuses.
        /// </summary>
        public async Task RemoveFromInboxAsync(int userId, int statusId)
        {
            await _dbContext.TrawellingInboxStatuses
                .Where(s => s.UserId == userId && s.TrawellingStatusId == statusId)
                .ExecuteDeleteAsync();
        }

        public async Task<List<TrawellingConflictDto>> GetConflictsAsync(User user)
        {
            var rows = await _dbContext.TrawellingInboxStatuses
                .Where(s => s.UserId == user.Id
                    && (s.State == TrawellingInboxState.ChangedAfterImport || s.State == TrawellingInboxState.DeletedUpstream))
                .OrderByDescending(s => s.LastEventAt)
                .ToListAsync();

            var conflicts = new List<TrawellingConflictDto>();
            foreach (var row in rows)
            {
                var conflict = await BuildConflictDtoAsync(user, row);
                if (conflict == null)
                {
                    // The user deleted the trip through the normal UI in the meantime;
                    // there is nothing left to resolve
                    _dbContext.TrawellingInboxStatuses.Remove(row);
                    continue;
                }
                if (row.State == TrawellingInboxState.ChangedAfterImport && !UpstreamDiffers(conflict))
                {
                    // Nothing the card can show has actually moved. Rows flagged before the
                    // webhook learned to tell a journey edit from a like are cleared here.
                    _dbContext.TrawellingInboxStatuses.Remove(row);
                    continue;
                }
                conflicts.Add(conflict);
            }
            await _dbContext.SaveChangesAsync();
            return conflicts;
        }

        /// <summary>Builds the conflict DTO for one row; null when its RouteInstance no longer exists.</summary>
        private async Task<TrawellingConflictDto> BuildConflictDtoAsync(User user, TrawellingInboxStatus row)
        {
            var instance = await FindImportedInstanceAsync(user, row.TrawellingStatusId);
            if (instance == null)
                return null;

            TrawellingTripDto newTrip = null;
            try
            {
                var status = JsonConvert.DeserializeObject<TrawellingStatus>(row.PayloadJson);
                newTrip = await MapStatusToTripDtoAsync(user, status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not parse conflict payload for status {StatusId}, user {UserId}", row.TrawellingStatusId, user.Id);
            }

            var hasSiblingInstances = await _dbContext.RouteInstances
                .AnyAsync(ri => ri.RouteId == instance.RouteId && ri.RouteInstanceId != instance.RouteInstanceId);

            return new TrawellingConflictDto
            {
                StatusId = row.TrawellingStatusId,
                State = row.State.ToString(),
                LastEventAt = row.LastEventAt,
                RouteInstanceId = instance.RouteInstanceId,
                RouteName = instance.Route.Name,
                RouteFrom = instance.Route.From,
                RouteTo = instance.Route.To,
                InstanceDate = instance.Date,
                InstanceStartTime = instance.StartTime,
                InstanceEndTime = instance.EndTime,
                IsLastInstanceOnRoute = !hasSiblingInstances,
                NewTrip = newTrip,
            };
        }

        public async Task<bool> ApplyConflictTimesAsync(User user, int statusId)
        {
            var row = await GetConflictRowAsync(user, statusId, TrawellingInboxState.ChangedAfterImport);
            var instance = await FindImportedInstanceAsync(user, statusId);
            if (row == null || instance == null)
                return false;

            var status = JsonConvert.DeserializeObject<TrawellingStatus>(row.PayloadJson);
            var trip = await MapStatusToTripDtoAsync(user, status);
            if (trip?.Transport == null)
                return false;

            // Same semantics as linking a status: real times win, scheduled as fallback,
            // all already converted to local time by the mapping
            instance.StartTime = trip.Transport.Origin.DepartureReal ?? trip.Transport.Origin.DepartureScheduled;
            instance.EndTime = trip.Transport.Destination.ArrivalReal ?? trip.Transport.Destination.ArrivalScheduled;
            instance.ScheduledStartTime = trip.Transport.Origin.DepartureScheduled;
            instance.ScheduledEndTime = trip.Transport.Destination.ArrivalScheduled;
            if (instance.StartTime.HasValue)
                instance.Date = instance.StartTime.Value.Date;
            if (instance.StartTime.HasValue && instance.EndTime.HasValue)
            {
                instance.DurationHours = _timezoneService.CalculateDurationInHours(
                    instance.StartTime.Value,
                    instance.EndTime.Value,
                    instance.Route.LineString);
            }

            _dbContext.TrawellingInboxStatuses.Remove(row);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Applied upstream time changes of status {StatusId} to RouteInstance {RouteInstanceId}", statusId, instance.RouteInstanceId);
            return true;
        }

        public async Task<bool> ReimportConflictAsync(User user, int statusId)
        {
            var row = await GetConflictRowAsync(user, statusId, TrawellingInboxState.ChangedAfterImport);
            var instance = await FindImportedInstanceAsync(user, statusId);
            if (row == null || instance == null)
                return false;

            // Unlink and hand the status back to the unimported list for a fresh import
            instance.TrawellingStatusId = null;
            row.State = TrawellingInboxState.Pending;
            row.LastEventAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Unlinked status {StatusId} from RouteInstance {RouteInstanceId} for re-import", statusId, instance.RouteInstanceId);
            return true;
        }

        public async Task<bool> DismissConflictAsync(User user, int statusId)
        {
            var row = await _dbContext.TrawellingInboxStatuses
                .SingleOrDefaultAsync(s => s.UserId == user.Id && s.TrawellingStatusId == statusId
                    && (s.State == TrawellingInboxState.ChangedAfterImport || s.State == TrawellingInboxState.DeletedUpstream));
            if (row == null)
                return false;

            if (row.State == TrawellingInboxState.DeletedUpstream)
            {
                // The status is gone upstream, no further events can arrive — nothing to remember
                _dbContext.TrawellingInboxStatuses.Remove(row);
            }
            else
            {
                // Keep the dismissed payload so the same change can't re-flag (fingerprint check)
                row.State = TrawellingInboxState.ConflictDismissed;
                row.LastEventAt = DateTime.UtcNow;
            }
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteInstanceForConflictAsync(User user, int statusId)
        {
            var row = await GetConflictRowAsync(user, statusId, TrawellingInboxState.DeletedUpstream);
            var instance = await FindImportedInstanceAsync(user, statusId);
            if (row == null || instance == null)
                return false;

            var hasSiblingInstances = await _dbContext.RouteInstances
                .AnyAsync(ri => ri.RouteId == instance.RouteId && ri.RouteInstanceId != instance.RouteInstanceId);
            if (hasSiblingInstances)
            {
                _dbContext.RouteInstances.Remove(instance);
            }
            else
            {
                // The route's only trip: an instance-less route is dead weight, remove it
                // along with the instance (announced in the confirmation dialog via
                // IsLastInstanceOnRoute on the conflict DTO)
                _dbContext.Routes.Remove(instance.Route);
            }

            _dbContext.TrawellingInboxStatuses.Remove(row);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Deleted RouteInstance {RouteInstanceId}{RouteNote} following upstream deletion of status {StatusId}",
                instance.RouteInstanceId, hasSiblingInstances ? "" : " and its route " + instance.RouteId, statusId);
            return true;
        }

        private Task<TrawellingInboxStatus> GetConflictRowAsync(User user, int statusId, TrawellingInboxState state)
            => _dbContext.TrawellingInboxStatuses
                .SingleOrDefaultAsync(s => s.UserId == user.Id && s.TrawellingStatusId == statusId && s.State == state);

        private Task<RouteInstance> FindImportedInstanceAsync(User user, int statusId)
            => _dbContext.RouteInstances
                .Include(ri => ri.Route)
                .Where(ri => ri.TrawellingStatusId == statusId)
                .Where(ri => ri.RouteInstanceMaps.Any(rim => rim.Map.UserId == user.Id)
                    || ri.Route.RouteMaps.Any(rm => rm.Map.UserId == user.Id))
                .FirstOrDefaultAsync();

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

        public async Task ProcessWebhookEventAsync(User user, string payloadJson)
        {
            var envelope = JObject.Parse(payloadJson);
            var eventType = envelope["event"]?.Value<string>();
            var statusToken = envelope["status"];
            if (string.IsNullOrEmpty(eventType) || statusToken is not JObject)
            {
                _logger.LogDebug("Träwelling webhook event {Event} without status payload ignored for user {UserId}", eventType, user.Id);
                return;
            }

            var status = statusToken.ToObject<TrawellingStatus>();
            if (status == null || status.Id == 0)
                return;
            var statusJson = statusToken.ToString(Formatting.None);
            var statusId = status.Id;

            // The delivery payload omits relations that happened not to be loaded upstream —
            // notably the stopovers' station objects, which the trip mapping requires. Treat
            // the delivery as a trigger and fetch the complete status; keep the delivery
            // payload only as a fallback when the fetch fails.
            if (eventType is "checkin_create" or "checkin_update")
            {
                var (fullJson, fullStatus) = await FetchStatusRawAsync(user, statusId);
                if (fullJson != null)
                {
                    statusJson = fullJson;
                    status = fullStatus;
                }
                else
                {
                    _logger.LogWarning("Could not fetch full status {StatusId} for user {UserId}, storing the webhook delivery payload", statusId, user.Id);
                }
            }

            var imported = await _dbContext.RouteInstances
                .Where(ri => ri.TrawellingStatusId == statusId)
                .Where(ri => ri.RouteInstanceMaps.Any(rim => rim.Map.UserId == user.Id)
                    || ri.Route.RouteMaps.Any(rm => rm.Map.UserId == user.Id))
                .AnyAsync();
            var ignored = await _dbContext.TrawellingIgnoredStatuses
                .AnyAsync(tis => tis.UserId == user.Id && tis.TrawellingStatusId == statusId);
            var inboxRow = await _dbContext.TrawellingInboxStatuses
                .SingleOrDefaultAsync(s => s.UserId == user.Id && s.TrawellingStatusId == statusId);

            var pendingUpserted = false;
            var pendingRemoved = false;
            TrawellingInboxStatus conflictRow = null;
            var conflictRemoved = false;

            switch (eventType)
            {
                case "checkin_create":
                case "checkin_update":
                    if (ignored)
                        return; // the user's decision stands
                    if (imported)
                    {
                        // Curated data is never auto-changed; flag the upstream edit for review
                        if (eventType == "checkin_update")
                        {
                            if (inboxRow?.State == TrawellingInboxState.ConflictDismissed
                                && DismissedFingerprintMatches(inboxRow, status))
                            {
                                break; // the same change the user already dismissed — don't nag
                            }
                            if (!await UpstreamDiffersAsync(user, statusId, status))
                            {
                                // A like, a tag or a body edit: nothing OVDB holds has moved, so
                                // there is nothing to review.
                                if (inboxRow?.State == TrawellingInboxState.ChangedAfterImport)
                                {
                                    // An earlier edit was undone upstream, or the user applied it
                                    // elsewhere — either way the flag has nothing left to say
                                    _dbContext.TrawellingInboxStatuses.Remove(inboxRow);
                                    conflictRemoved = true;
                                }
                                break;
                            }
                            conflictRow = UpsertInboxRow(inboxRow, user.Id, statusId, statusJson, status, TrawellingInboxState.ChangedAfterImport);
                        }
                        break;
                    }
                    // Pending or unknown: (re)store as pending — an update for a status we
                    // never saw doubles as the missed create
                    UpsertInboxRow(inboxRow, user.Id, statusId, statusJson, status, TrawellingInboxState.Pending);
                    pendingUpserted = true;
                    break;

                case "checkin_delete":
                    if (imported)
                    {
                        // OVDB is the archive: flag it, never auto-delete the RouteInstance
                        conflictRow = UpsertInboxRow(inboxRow, user.Id, statusId, statusJson, status, TrawellingInboxState.DeletedUpstream);
                    }
                    else
                    {
                        if (inboxRow != null)
                        {
                            _dbContext.TrawellingInboxStatuses.Remove(inboxRow);
                            pendingRemoved = inboxRow.State == TrawellingInboxState.Pending;
                        }
                        if (ignored)
                        {
                            // Moot now that the status is gone upstream
                            var ignoreRows = await _dbContext.TrawellingIgnoredStatuses
                                .Where(tis => tis.UserId == user.Id && tis.TrawellingStatusId == statusId)
                                .ToListAsync();
                            _dbContext.TrawellingIgnoredStatuses.RemoveRange(ignoreRows);
                        }
                    }
                    break;

                default:
                    return; // not subscribed to anything else; ignore defensively
            }

            await _dbContext.SaveChangesAsync();
            // The 30-minute trip DTO cache would otherwise serve the pre-update payload
            _memoryCache.Remove($"TrawellingTrip|{statusId}");
            _logger.LogInformation("Processed Träwelling webhook {Event} for status {StatusId}, user {UserId}", eventType, statusId, user.Id);

            await PublishPendingTripChangeAsync(user, statusId, status, pendingUpserted, pendingRemoved);
            await PublishConflictChangeAsync(user, conflictRow);
            if (conflictRemoved)
            {
                await PublishConflictRemovedAsync(user, statusId);
            }
        }

        /// <summary>
        /// Pushes a new or updated conflict to the owning user's open pages so the
        /// "Changed on Träwelling" section updates without a reload.
        /// </summary>
        private async Task PublishConflictChangeAsync(User user, TrawellingInboxStatus conflictRow)
        {
            if (conflictRow == null)
                return;
            try
            {
                var conflict = await BuildConflictDtoAsync(user, conflictRow);
                if (conflict == null)
                    return;
                await _traewellingHubContext.Clients.User(user.Id.ToString())
                    .SendAsync(TraewellingHub.ConflictUpsertedMethod, JsonConvert.SerializeObject(conflict, ApiJsonSettings));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing live Träwelling conflict for status {StatusId}, user {UserId}", conflictRow.TrawellingStatusId, user.Id);
            }
        }

        /// <summary>
        /// Takes a conflict card off the open pages once it has nothing left to report.
        /// </summary>
        private async Task PublishConflictRemovedAsync(User user, int statusId)
        {
            try
            {
                await _traewellingHubContext.Clients.User(user.Id.ToString())
                    .SendAsync(TraewellingHub.ConflictRemovedMethod, statusId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing live Träwelling conflict removal for status {StatusId}, user {UserId}", statusId, user.Id);
            }
        }

        /// <summary>
        /// Pushes a live update for the unimported-trips list to the owning user's open
        /// pages. Only pending-list changes are pushed; the conflict states get their UI
        /// in a later phase. Delivery failures are logged and ignored — the list is
        /// DB-backed, so a missed push only means the change appears on the next load.
        /// </summary>
        private async Task PublishPendingTripChangeAsync(User user, int statusId, TrawellingStatus status, bool pendingUpserted, bool pendingRemoved)
        {
            try
            {
                if (pendingUpserted)
                {
                    var trip = await MapStatusToTripDtoAsync(user, status);
                    if (trip == null)
                        return;
                    _memoryCache.Set($"TrawellingTrip|{statusId}", trip, TimeSpan.FromMinutes(30));
                    await _traewellingHubContext.Clients.User(user.Id.ToString())
                        .SendAsync(TraewellingHub.PendingTripUpsertedMethod, JsonConvert.SerializeObject(trip, ApiJsonSettings));
                }
                else if (pendingRemoved)
                {
                    await _traewellingHubContext.Clients.User(user.Id.ToString())
                        .SendAsync(TraewellingHub.PendingTripRemovedMethod, statusId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing live Träwelling update for status {StatusId}, user {UserId}", statusId, user.Id);
            }
        }

        /// <summary>
        /// Fetch a single status from the API as raw JSON plus its parsed form. Unlike the
        /// webhook delivery payload, this endpoint returns the complete resource including
        /// the stopovers' station objects.
        /// </summary>
        private async Task<(string Json, TrawellingStatus Status)> FetchStatusRawAsync(User user, int statusId)
        {
            try
            {
                if (!await EnsureValidTokenAsync(user))
                    return (null, null);

                var response = await SendAsync(() =>
                    CreateApiRequest(HttpMethod.Get, $"{_baseUrl}/status/{statusId}", user));
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

        private bool DismissedFingerprintMatches(TrawellingInboxStatus dismissedRow, TrawellingStatus incoming)
        {
            try
            {
                var dismissed = JsonConvert.DeserializeObject<TrawellingStatus>(dismissedRow.PayloadJson);
                return dismissed != null && ConflictFingerprint(dismissed) == ConflictFingerprint(incoming);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// The journey facts a conflict flag is about: line and the stations/times of both
        /// ends. Social fields (likes, tags, body) deliberately don't count — a change there
        /// must not re-flag a dismissed conflict.
        /// </summary>
        internal static string ConflictFingerprint(TrawellingStatus status)
        {
            var checkin = status?.Checkin;
            return string.Join("|",
                checkin?.LineName,
                checkin?.Origin?.Station?.Name,
                checkin?.Destination?.Station?.Name,
                checkin?.ManualDeparture?.UtcDateTime.ToString("o"),
                checkin?.Origin?.DeparturePlanned?.UtcDateTime.ToString("o"),
                checkin?.Origin?.DepartureReal?.UtcDateTime.ToString("o"),
                checkin?.ManualArrival?.UtcDateTime.ToString("o"),
                checkin?.Destination?.ArrivalPlanned?.UtcDateTime.ToString("o"),
                checkin?.Destination?.ArrivalReal?.UtcDateTime.ToString("o"));
        }

        /// <summary>
        /// Whether the upstream status still says something different from the RouteInstance that
        /// was imported from it. Träwelling fires <c>checkin_update</c> for likes, tags, body edits
        /// and visibility changes as well as for journey edits, and a notice about a trip whose
        /// four comparable values all match asks the user to spot a difference that isn't there.
        /// </summary>
        /// <remarks>
        /// Deliberately the same four comparisons, with the same tolerances, that the conflict card
        /// renders: if the card cannot show a difference, there is no conflict to raise. A status
        /// that cannot be mapped counts as different — losing a real upstream edit is the worse
        /// failure of the two.
        /// </remarks>
        internal static bool UpstreamDiffers(RouteInstance instance, TrawellingTripDto newTrip)
            => UpstreamDiffers(instance.Route?.From, instance.Route?.To, instance.StartTime, instance.EndTime, newTrip);

        /// <summary>The same check against a conflict already built for the UI.</summary>
        internal static bool UpstreamDiffers(TrawellingConflictDto conflict)
            => UpstreamDiffers(conflict.RouteFrom, conflict.RouteTo, conflict.InstanceStartTime, conflict.InstanceEndTime, conflict.NewTrip);

        private static bool UpstreamDiffers(string currentFrom, string currentTo, DateTime? currentStart, DateTime? currentEnd, TrawellingTripDto newTrip)
        {
            var transport = newTrip?.Transport;
            if (transport == null)
            {
                return true;
            }

            return StationsDiffer(currentFrom, transport.Origin?.Name)
                || StationsDiffer(currentTo, transport.Destination?.Name)
                || TimesDiffer(currentStart, transport.Origin?.DepartureReal ?? transport.Origin?.DepartureScheduled)
                || TimesDiffer(currentEnd, transport.Destination?.ArrivalReal ?? transport.Destination?.ArrivalScheduled);
        }

        /// <summary>Minute precision: sub-minute serialisation noise is not a change.</summary>
        private static bool TimesDiffer(DateTime? current, DateTime? upstream)
        {
            if (!current.HasValue || !upstream.HasValue)
            {
                return current.HasValue != upstream.HasValue;
            }
            return current.Value.Ticks / TimeSpan.TicksPerMinute != upstream.Value.Ticks / TimeSpan.TicksPerMinute;
        }

        /// <summary>
        /// OVDB route endpoints are user-editable text, so only a clear difference between two
        /// known names counts — an endpoint the user renamed is not an upstream edit.
        /// </summary>
        private static bool StationsDiffer(string current, string upstream)
        {
            if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(upstream))
            {
                return false;
            }
            static string Normalise(string value) => string.Join(' ',
                value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
            return Normalise(current) != Normalise(upstream);
        }

        /// <summary>
        /// <see cref="UpstreamDiffers"/> for a status that still has to be looked up and mapped.
        /// </summary>
        private async Task<bool> UpstreamDiffersAsync(User user, int statusId, TrawellingStatus status)
        {
            var instance = await FindImportedInstanceAsync(user, statusId);
            if (instance == null)
            {
                return false; // nothing imported to conflict with
            }
            return UpstreamDiffers(instance, await MapStatusToTripDtoAsync(user, status));
        }

        private TrawellingInboxStatus UpsertInboxRow(TrawellingInboxStatus row, int userId, int statusId, string payloadJson, TrawellingStatus status, TrawellingInboxState state)
        {
            if (row == null)
            {
                row = new TrawellingInboxStatus
                {
                    UserId = userId,
                    TrawellingStatusId = statusId,
                    ReceivedAt = DateTime.UtcNow,
                };
                _dbContext.TrawellingInboxStatuses.Add(row);
            }
            row.PayloadJson = payloadJson;
            row.State = state;
            row.Source = TrawellingInboxSource.Webhook;
            row.DepartureAt = GetStatusDeparture(status);
            row.LastEventAt = DateTime.UtcNow;
            return row;
        }

        public async Task<bool> IgnoreStatusAsync(User user, int statusId)
        {
            try
            {
                // Check if already ignored
                var existingIgnore = await _dbContext.TrawellingIgnoredStatuses
                    .AnyAsync(tis => tis.UserId == user.Id && tis.TrawellingStatusId == statusId);

                if (existingIgnore)
                {
                    _logger.LogInformation("Status {StatusId} is already ignored by user {UserId}", statusId, user.Id);
                    return false; // Already ignored
                }

                // Add to ignored statuses
                var ignoredStatus = new TrawellingIgnoredStatus
                {
                    UserId = user.Id,
                    TrawellingStatusId = statusId,
                    IgnoredAt = DateTime.UtcNow
                };

                _dbContext.TrawellingIgnoredStatuses.Add(ignoredStatus);
                await _dbContext.SaveChangesAsync();
                await RemoveFromInboxAsync(user.Id, statusId);

                _logger.LogInformation("Successfully ignored status {StatusId} for user {UserId}", statusId, user.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ignoring status {StatusId} for user {UserId}", statusId, user.Id);
                return false;
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

        private async Task<Route> FindOrCreateRouteAsync(TrawellingStatus status)
        {
            try
            {
                var transport = status.Checkin;
                if (transport?.Origin?.Station == null || transport?.Destination?.Station == null)
                {
                    _logger.LogWarning("Status {StatusId} missing origin or destination data", status.Id);
                    return null;
                }

                var originName = transport.Origin.Station.Name;
                var destinationName = transport.Destination.Station.Name;
                var lineName = transport.LineName ?? $"{transport.Category} {transport.Number}";

                // Try to find existing route by name pattern
                var routeName = $"{originName} - {destinationName}";
                var existingRoute = await _dbContext.Routes
                    .FirstOrDefaultAsync(r => r.Name == routeName ||
                                            (r.From == originName && r.To == destinationName));

                if (existingRoute != null)
                {
                    _logger.LogInformation("Found existing route {RouteId} for {Origin} to {Destination}",
                        existingRoute.RouteId, originName, destinationName);
                    return existingRoute;
                }

                // Create new route
                var newRoute = new Route
                {
                    Name = routeName,
                    From = originName,
                    To = destinationName,
                    Description = $"Imported from Träwelling - {lineName}",
                    LineNumber = lineName,
                    OperatingCompany = "Imported from Träwelling",
                    FirstDateTime = status.CreatedAt.DateTime,
                    Share = Guid.NewGuid(),
                    CalculatedDistance = transport.Distance > 0 ? transport.Distance / 1000.0 : 0 // Convert meters to km
                };

                _dbContext.Routes.Add(newRoute);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Created new route {RouteId} for {Origin} to {Destination}",
                    newRoute.RouteId, originName, destinationName);

                return newRoute;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding or creating route for status {StatusId}", status.Id);
                return null;
            }
        }

        /// <summary>
        /// Try to update existing RouteInstances with timing data from Träwelling
        /// This is useful for RouteInstances that were added before the timing feature existed
        /// </summary>
        private async Task<bool> UpdateExistingRouteInstanceWithTrawellingDataAsync(User user, TrawellingStatus status)
        {
            try
            {
                var transport = status.Checkin;
                if (transport?.Origin?.Station == null || transport?.Destination?.Station == null)
                    return false;

                var originName = transport.Origin.Station.Name;
                var destinationName = transport.Destination.Station.Name;
                var tripDate = status.CreatedAt.Date;

                // Find RouteInstances on the same date that might match this trip
                var candidateRoutes = await _dbContext.Routes
                    .Where(r => (r.From == originName && r.To == destinationName) ||
                               r.Name.Contains(originName) || r.Name.Contains(destinationName))
                    .ToListAsync();

                if (!candidateRoutes.Any())
                    return false;

                var routeIds = candidateRoutes.Select(r => r.RouteId).ToList();
                var candidateInstances = await _dbContext.RouteInstances
                    .Include(ri => ri.RouteInstanceProperties)
                    .Where(ri => routeIds.Contains(ri.RouteId) &&
                                ri.Date.Date == tripDate &&
                                ri.TrawellingStatusId == null && // Not already linked to Träwelling
                                (!ri.StartTime.HasValue || !ri.EndTime.HasValue)) // Missing timing data
                    .ToListAsync();

                if (!candidateInstances.Any())
                    return false;

                // Find the best match - prefer exact route match, then by route name similarity
                var bestMatch = candidateInstances
                    .OrderByDescending(ri =>
                        candidateRoutes.First(r => r.RouteId == ri.RouteId).From == originName &&
                        candidateRoutes.First(r => r.RouteId == ri.RouteId).To == destinationName)
                    .ThenByDescending(ri =>
                        candidateRoutes.First(r => r.RouteId == ri.RouteId).Name
                            .Split(new[] { " - ", "-" }, StringSplitOptions.RemoveEmptyEntries)
                            .Count(part => originName.Contains(part, StringComparison.OrdinalIgnoreCase) ||
                                          destinationName.Contains(part, StringComparison.OrdinalIgnoreCase)))
                    .FirstOrDefault();

                if (bestMatch == null)
                    return false;

                // Update with Träwelling data
                var updated = false;

                var effectiveDeparture = transport.Origin.DepartureReal ?? transport.Origin.DeparturePlanned;
                if (!bestMatch.StartTime.HasValue && effectiveDeparture.HasValue)
                {
                    bestMatch.StartTime = effectiveDeparture.Value.DateTime;
                    updated = true;
                }

                if (!bestMatch.ScheduledStartTime.HasValue && transport.Origin.DeparturePlanned.HasValue)
                {
                    bestMatch.ScheduledStartTime = transport.Origin.DeparturePlanned.Value.DateTime;
                    updated = true;
                }

                var effectiveArrival = transport.Destination.ArrivalReal ?? transport.Destination.ArrivalPlanned;
                if (!bestMatch.EndTime.HasValue && effectiveArrival.HasValue)
                {
                    bestMatch.EndTime = effectiveArrival.Value.DateTime;
                    updated = true;
                }

                if (!bestMatch.ScheduledEndTime.HasValue && transport.Destination.ArrivalPlanned.HasValue)
                {
                    bestMatch.ScheduledEndTime = transport.Destination.ArrivalPlanned.Value.DateTime;
                    updated = true;
                }

                // Calculate duration if we now have both times
                if (bestMatch.StartTime.HasValue && bestMatch.EndTime.HasValue && !bestMatch.DurationHours.HasValue)
                {
                    var duration = bestMatch.EndTime.Value - bestMatch.StartTime.Value;
                    bestMatch.DurationHours = duration.TotalHours;
                    updated = true;
                }

                // Link to Träwelling status
                bestMatch.TrawellingStatusId = status.Id;
                updated = true;
                await RemoveFromInboxAsync(user.Id, status.Id);

                // Add Träwelling metadata properties if they don't exist
                var existingKeys = bestMatch.RouteInstanceProperties?.Select(p => p.Key).ToHashSet() ?? new HashSet<string>();
                var newProperties = new List<RouteInstanceProperty>();

                if (!existingKeys.Contains("traewelling_line") && !string.IsNullOrEmpty(transport.LineName))
                {
                    newProperties.Add(new RouteInstanceProperty
                    {
                        RouteInstanceId = bestMatch.RouteInstanceId,
                        Key = "traewelling_line",
                        Value = transport.LineName
                    });
                }

                if (!existingKeys.Contains("source"))
                {
                    newProperties.Add(new RouteInstanceProperty
                    {
                        RouteInstanceId = bestMatch.RouteInstanceId,
                        Key = "source",
                        Value = "traewelling_backfill"
                    });
                }

                if (newProperties.Any())
                {
                    _dbContext.RouteInstanceProperties.AddRange(newProperties);
                    updated = true;
                }

                if (updated)
                {
                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation("Updated RouteInstance {RouteInstanceId} with Träwelling data from status {StatusId}",
                        bestMatch.RouteInstanceId, status.Id);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating existing RouteInstance with Träwelling data from status {StatusId}", status.Id);
                return false;
            }
        }

        public async Task<List<RouteInstance>> GetRouteInstancesByDateAsync(User user, DateTime date, string searchQuery = null)
        {
            try
            {
                var query = _dbContext.RouteInstances
                    .Include(ri => ri.Route)
                    .Where(ri => !ri.TrawellingStatusId.HasValue)
                    .Where(ri => ri.Route.RouteMaps.Any(rm => rm.Map.UserId == user.Id))
                    .Where(ri => ri.Date.Date == date.Date);



                //if (!string.IsNullOrWhiteSpace(searchQuery))
                //{
                //    query = query.Where(ri => ri.Route.Name.Contains(searchQuery) ||
                //                            ri.Route.From.Contains(searchQuery) ||
                //                            ri.Route.To.Contains(searchQuery));
                //}

                var routeInstances = await query
                    .OrderBy(ri => ri.TrawellingStatusId.HasValue)
                    .ThenByDescending(ri => ri.StartTime ?? ri.Date)
                    .ThenByDescending(ri => ri.RouteInstanceId)
                    .ToListAsync();

                _logger.LogInformation("Found {Count} RouteInstances for date {Date} with query '{Query}'",
                    routeInstances.Count, date.Date, searchQuery ?? "none");

                return routeInstances;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching RouteInstances for date {Date} with query '{Query}'", date, searchQuery);
                return new List<RouteInstance>();
            }
        }

        public async Task<RouteInstance> LinkStatusToRouteInstanceAsync(User user, int statusId, int routeInstanceId)
        {
            try
            {
                // Check if the Träwelling status is already linked to another RouteInstance
                var existingLink = await _dbContext.RouteInstances
                    .FirstOrDefaultAsync(ri => ri.TrawellingStatusId == statusId);

                if (existingLink != null)
                {
                    _logger.LogWarning("Träwelling status {StatusId} is already linked to RouteInstance {ExistingRouteInstanceId}",
                        statusId, existingLink.RouteInstanceId);
                    return null;
                }

                // Get the target RouteInstance
                var routeInstance = await _dbContext.RouteInstances
                    .Include(ri => ri.Route)
                    .FirstOrDefaultAsync(ri => ri.RouteInstanceId == routeInstanceId);

                if (routeInstance == null)
                {
                    _logger.LogWarning("RouteInstance {RouteInstanceId} not found", routeInstanceId);
                    return null;
                }

                // Get the Träwelling status to potentially update timing data
                var statusData = await GetStatusAsync(user, statusId);
                if (statusData != null && routeInstance.StartTime == null && routeInstance.EndTime == null)
                {

                    routeInstance.StartTime = statusData.Transport.Origin.DepartureReal ?? statusData.Transport.Origin.DepartureScheduled;

                    routeInstance.EndTime = statusData.Transport.Destination.ArrivalReal ?? statusData.Transport.Destination.ArrivalScheduled;

                    routeInstance.ScheduledStartTime = statusData.Transport.Origin.DepartureScheduled;
                    routeInstance.ScheduledEndTime = statusData.Transport.Destination.ArrivalScheduled;

                    if (routeInstance.StartTime.HasValue && routeInstance.EndTime.HasValue)
                    {
                        routeInstance.DurationHours = _timezoneService.CalculateDurationInHours(
                            routeInstance.StartTime.Value,
                            routeInstance.EndTime.Value,
                            routeInstance.Route.LineString);
                    }
                }

                // Link the status
                routeInstance.TrawellingStatusId = statusId;
                await _dbContext.SaveChangesAsync();
                await RemoveFromInboxAsync(user.Id, statusId);

                _logger.LogInformation("Successfully linked Träwelling status {StatusId} to RouteInstance {RouteInstanceId}",
                    statusId, routeInstanceId);

                return routeInstance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error linking Träwelling status {StatusId} to RouteInstance {RouteInstanceId}",
                    statusId, routeInstanceId);
                return null;
            }
        }

        private async Task<TrawellingTripDto> GetStatusAsync(User user, int statusId)
        {
            try
            {

                if (_memoryCache.TryGetValue($"TrawellingTrip|{statusId}", out TrawellingTripDto cachedStatus))
                {
                    return cachedStatus;
                }


                // Cache miss or expired - fetch from API
                if (!await EnsureValidTokenAsync(user))
                    return null;

                var response = await SendAsync(() =>
                    CreateApiRequest(HttpMethod.Get, $"{_baseUrl}/status/{statusId}", user));

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var statusResponse = JsonConvert.DeserializeObject<TrawellingStatusResponse>(content);

                var status = statusResponse?.Data;
                var mapped = await MapStatusToTripDtoAsync(user, status);
                if (status != null)
                {
                    _memoryCache.Set($"TrawellingTrip|{statusId}", mapped, TimeSpan.FromMinutes(30));
                }

                return mapped;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Träwelling status {StatusId}", statusId);
                return null;
            }
        }

        private async Task<TrawellingTripDto> MapStatusToTripDtoAsync(User user, TrawellingStatus status)
        {
            try
            {
                var transport = status.Checkin;
                if (transport?.Origin?.Station == null || transport?.Destination?.Station == null)
                    return null;

                var origin = await MapStopoverToDto(transport.Origin, transport.ManualDeparture?.UtcDateTime, isArrival: false);
                var destination = await MapStopoverToDto(transport.Destination, transport.ManualArrival?.UtcDateTime, isArrival: true);

                return new TrawellingTripDto
                {
                    Id = status.Id,
                    Body = status.Body,
                    Business = status.Business,
                    Visibility = status.Visibility,
                    CreatedAt = status.CreatedAt,
                    Transport = new TrawellingTransportDto
                    {
                        Category = transport.Category,
                        Number = transport.Number,
                        LineName = transport.LineName,
                        JourneyNumber = !string.IsNullOrWhiteSpace(transport.ManualJourneyNumber) ? transport.ManualJourneyNumber : transport.JourneyNumber.ToString(),
                        Distance = transport.Distance,
                        Duration = transport.Duration,
                        Origin = origin,
                        Destination = destination,
                        Operator = transport.Operator != null ? new TrawellingOperatorDto
                        {
                            Name = transport.Operator.Name
                        } : null
                    },
                    UserDetails = status.User != null ? new TrawellingLightUserDto
                    {
                        Id = status.User.Id,
                        DisplayName = status.User.DisplayName,
                        Username = status.User.Username,
                        ProfilePicture = status.User.ProfilePicture
                    } : null,
                    Tags = status.Tags?.Select(t => new TrawellingStatusTagDto
                    {
                        Key = t.Key,
                        Value = t.Value,
                        Visibility = t.Visibility
                    }).ToList() ?? new List<TrawellingStatusTagDto>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapping status {StatusId} to trip DTO", status.Id);
                return null;
            }
        }

        private async Task<TrawellingStopoverDto> MapStopoverToDto(TrawellingStopover stopover, DateTime? manualTime, bool isArrival)
        {
            var station = stopover.Station;
            try
            {
                // Determine the best real times (manual times take precedence)
                DateTime? realArrival = null;
                DateTime? realDeparture = null;

                if (isArrival)
                {
                    realArrival = manualTime ?? (stopover.ArrivalReal ?? stopover.ArrivalPlanned)?.UtcDateTime;
                }

                else
                {
                    realDeparture = manualTime ?? (stopover.DepartureReal ?? stopover.DeparturePlanned)?.UtcDateTime;
                }

                // Convert UTC times to local timezone if we have coordinates
                DateTime? localArrivalScheduled = null;
                DateTime? localDepartureScheduled = null;
                DateTime? localArrivalReal = null;
                DateTime? localDepartureReal = null;

                if (station.Latitude.HasValue && station.Longitude.HasValue)
                {
                    if (stopover.ArrivalPlanned.HasValue)
                        localArrivalScheduled = await _timezoneService.ConvertUtcToLocalTimeAsync(stopover.ArrivalPlanned.Value.UtcDateTime, station.Latitude.Value, station.Longitude.Value);

                    if (stopover.DeparturePlanned.HasValue)
                        localDepartureScheduled = await _timezoneService.ConvertUtcToLocalTimeAsync(stopover.DeparturePlanned.Value.UtcDateTime, station.Latitude.Value, station.Longitude.Value);

                    if (realArrival.HasValue)
                        localArrivalReal = await _timezoneService.ConvertUtcToLocalTimeAsync(realArrival.Value, station.Latitude.Value, station.Longitude.Value);

                    if (realDeparture.HasValue)
                        localDepartureReal = await _timezoneService.ConvertUtcToLocalTimeAsync(realDeparture.Value, station.Latitude.Value, station.Longitude.Value);
                }
                else
                {
                    // Fallback to UTC times if no coordinates available
                    localArrivalScheduled = stopover.ArrivalPlanned?.UtcDateTime;
                    localDepartureScheduled = stopover.DeparturePlanned?.UtcDateTime;
                    localArrivalReal = realArrival;
                    localDepartureReal = realDeparture;

                    _logger.LogWarning("No coordinates available for station {StationName}, using UTC times", station.Name);
                }

                return new TrawellingStopoverDto
                {
                    Name = station.Name,
                    ArrivalScheduled = localArrivalScheduled,
                    DepartureScheduled = localDepartureScheduled,
                    ArrivalPlatformPlanned = stopover.ArrivalPlatformPlanned,
                    DeparturePlatformPlanned = stopover.DeparturePlatformPlanned,
                    ArrivalReal = localArrivalReal,
                    DepartureReal = localDepartureReal,
                    ArrivalPlatformReal = stopover.ArrivalPlatformReal,
                    DeparturePlatformReal = stopover.DeparturePlatformReal,
                    IsArrivalDelayed = stopover.IsArrivalDelayed,
                    IsDepartureDelayed = stopover.IsDepartureDelayed,
                    Cancelled = stopover.Cancelled,
                    Platform = stopover.Platform
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapping stopover at {StationName} to DTO", station?.Name);
                // Return basic DTO without timezone conversion on error
                return new TrawellingStopoverDto
                {
                    Name = station?.Name,
                    ArrivalScheduled = stopover.ArrivalPlanned?.DateTime,
                    DepartureScheduled = stopover.DeparturePlanned?.DateTime,
                    ArrivalReal = (stopover.ArrivalReal ?? stopover.ArrivalPlanned)?.DateTime,
                    DepartureReal = (stopover.DepartureReal ?? stopover.DeparturePlanned)?.DateTime,
                    IsArrivalDelayed = stopover.IsArrivalDelayed,
                    IsDepartureDelayed = stopover.IsDepartureDelayed,
                    Cancelled = stopover.Cancelled
                };
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

        public async Task<(int found, int updated, int failed)> BackfillScheduledTimesAsync(User user)
        {
            var found = 0;
            var updated = 0;
            var failed = 0;

            try
            {
                var instances = await _dbContext.RouteInstances
                    .Include(ri => ri.Route)
                    .Where(ri => ri.TrawellingStatusId.HasValue &&
                                 ri.StartTime.HasValue &&
                                 !ri.ScheduledStartTime.HasValue)
                    .Where(ri => ri.RouteInstanceMaps.Any(rim => rim.Map.UserId == user.Id) || ri.Route.RouteMaps.Any(rm => rm.Map.UserId == user.Id))
                    .ToListAsync();

                found = instances.Count;
                _logger.LogInformation("Backfilling scheduled times for {Count} RouteInstances", found);

                foreach (var instance in instances)
                {
                    try
                    {
                        var statusData = await GetStatusAsync(user, instance.TrawellingStatusId.Value);
                        if (statusData?.Transport?.Origin == null || statusData.Transport.Destination == null)
                        {
                            failed++;
                            continue;
                        }

                        instance.ScheduledStartTime = statusData.Transport.Origin.DepartureScheduled;
                        instance.ScheduledEndTime = statusData.Transport.Destination.ArrivalScheduled;
                        updated++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error backfilling scheduled times for RouteInstance {Id}", instance.RouteInstanceId);
                        failed++;
                    }
                    await _dbContext.SaveChangesAsync();
                }

                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled times backfill for user {UserId}", user.Id);
            }

            return (found, updated, failed);
        }

        /// <summary>
        /// Central pipeline for every Träwelling HTTP call: waits for the shared rate-limit
        /// window when exhausted, builds a fresh request per attempt (auth header included,
        /// no shared-client header mutation), records rate-limit headers from every response
        /// and retries on 429/503.
        /// </summary>
        private static HttpRequestMessage CreateApiRequest(HttpMethod method, string url, User user)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", user.TrawellingAccessToken);
            return request;
        }

        /// <summary>
        /// Every station this trip calls at. See <see cref="ITrawellingService.GetTripStopoversAsync"/>
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
            catch (Exception ex)
            {
                // A missing calling pattern costs suggestions, not the import. Never fail the one
                // for the other.
                _logger.LogError(ex, "Error fetching stopovers for trip {TripId} for user {UserId}", tripId, user.Id);
                return [];
            }
        }

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
    }
}