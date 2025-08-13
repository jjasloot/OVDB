using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OV_DB.Models;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace OV_DB.Services
{
    public class TrawellingService : ITrawellingService
    {
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
        private readonly IMemoryCache _memoryCache;

        // Simple in-memory cache for OAuth states - in production, use Redis or database
        private static readonly Dictionary<string, (int UserId, DateTime Expiry)> _oauthStates = new();
        private static readonly object _statelock = new object();


        // Rate limiting tracking
        private int? _rateLimitLimit;
        private int? _rateLimitRemaining;
        private DateTime _rateLimitUpdated;


        public TrawellingService(HttpClient httpClient, IConfiguration configuration, ITimezoneService timezoneService,
            OVDBDatabaseContext dbContext, ILogger<TrawellingService> logger, IMemoryCache memoryCache)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _timezoneService = timezoneService;
            _dbContext = dbContext;
            _logger = logger;
            _memoryCache = memoryCache;
            _baseUrl = _configuration["Traewelling:BaseUrl"];
            _clientId = _configuration["Traewelling:ClientId"];
            _clientSecret = _configuration["Traewelling:ClientSecret"];
            _redirectUri = _configuration["Traewelling:RedirectUri"];
            _authorizeUrl = _configuration["Traewelling:AuthorizeUrl"];
            _tokenUrl = _configuration["Traewelling:TokenUrl"];
        }

        public string GetAuthorizationUrl(int userId, string state)
        {
            var queryParams = HttpUtility.ParseQueryString(string.Empty);
            queryParams["response_type"] = "code";
            queryParams["client_id"] = _clientId;
            queryParams["redirect_uri"] = _redirectUri;
            queryParams["scope"] = "read-statuses write-statuses";
            queryParams["state"] = state;

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
                var tokenRequest = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("client_id", _clientId),
                    new KeyValuePair<string, string>("client_secret", _clientSecret),
                    new KeyValuePair<string, string>("redirect_uri", _redirectUri),
                    new KeyValuePair<string, string>("code", code)
                });

                var response = await _httpClient.PostAsync(_tokenUrl, tokenRequest);

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

                var refreshRequest = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("client_id", _clientId),
                    new KeyValuePair<string, string>("client_secret", _clientSecret),
                    new KeyValuePair<string, string>("refresh_token", user.TrawellingRefreshToken)
                });

                var response = await _httpClient.PostAsync(_tokenUrl, refreshRequest);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to refresh tokens for user {UserId}. Status: {StatusCode}",
                        user.Id, response.StatusCode);
                    return false;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonConvert.DeserializeObject<TrawellingTokenResponse>(responseContent);

                user.TrawellingAccessToken = tokenResponse.AccessToken;
                user.TrawellingRefreshToken = tokenResponse.RefreshToken;
                user.TrawellingTokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

                await _dbContext.SaveChangesAsync();
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

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {user.TrawellingAccessToken}");

                var response = await _httpClient.GetAsync($"{_baseUrl}/auth/user");

                // Update rate limit tracking
                UpdateRateLimitInfo(response);

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

        public async Task<TrawellingStatusesResponse> GetUnimportedStatusesAsync(User user, int page = 1)
        {
            try
            {
                if (!await EnsureValidTokenAsync(user))
                    return null;

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {user.TrawellingAccessToken}");

                TrawellingStatusesResponse statusesResponse = null;
                int currentPage = page;
                
                // Loop through pages until we find one with unimported trips or reach the end
                do
                {
                    var response = await _httpClient.GetAsync($"{_baseUrl}/user/jjasloot/statuses?page={currentPage}");

                    // Update rate limit tracking
                    UpdateRateLimitInfo(response);

                    // Check for rate limiting and implement backoff
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        _logger.LogWarning("Rate limit hit for user {UserId}. Implementing backoff.", user.Id);
                        await Task.Delay(TimeSpan.FromMinutes(1)); // Simple backoff - wait 1 minute
                        response = await _httpClient.GetAsync($"{_baseUrl}/user/jjasloot/statuses?page={currentPage}");
                        UpdateRateLimitInfo(response);
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("Failed to get statuses for user {UserId}. Status: {StatusCode}",
                            user.Id, response.StatusCode);
                        return null;
                    }

                    var responseContent = await response.Content.ReadAsStringAsync();
                    statusesResponse = JsonConvert.DeserializeObject<TrawellingStatusesResponse>(responseContent);

                    if (statusesResponse?.Data != null)
                    {
                        foreach (var status in statusesResponse.Data)
                        {
                            _memoryCache.Set("TraewellingStatus|" + status.Id, status, TimeSpan.FromMinutes(30)); 
                        }

                        // Filter out statuses that are already imported or ignored
                        var existingTrawellingIds = await _dbContext.RouteInstances
                            .Where(ri => ri.TrawellingStatusId.HasValue)
                            .Select(ri => ri.TrawellingStatusId.Value)
                            .ToListAsync();

                        var ignoredTrawellingIds = await _dbContext.TrawellingIgnoredStatuses
                            .Where(tis => tis.UserId == user.Id)
                            .Select(tis => tis.TrawellingStatusId)
                            .ToListAsync();

                        var filteredData = statusesResponse.Data
                            .Where(status => !existingTrawellingIds.Contains(status.Id) &&
                                           !ignoredTrawellingIds.Contains(status.Id))
                            .ToList();

                        // If we found unimported trips or reached the original requested page, return
                        if (filteredData.Any() || currentPage == page)
                        {
                            statusesResponse.Data = filteredData;
                            return statusesResponse;
                        }

                        // If this page was empty but there are more pages, continue to next page
                        if (statusesResponse.Meta?.CurrentPage < statusesResponse.Meta?.Total)
                        {
                            currentPage++;
                            continue;
                        }
                        else
                        {
                            // No more pages, return empty result
                            statusesResponse.Data = new List<TrawellingStatus>();
                            return statusesResponse;
                        }
                    }

                    break;
                } while (statusesResponse?.Meta?.CurrentPage < statusesResponse?.Meta?.Total);

                return statusesResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unimported statuses for user {UserId}", user.Id);
                return null;
            }
        }

        public async Task<TrawellingTripsResponse> GetOptimizedTripsAsync(User user, int page = 1)
        {
            try
            {
                var statusesResponse = await GetUnimportedStatusesAsync(user, page);
                if (statusesResponse?.Data == null)
                    return null;

                var optimizedTrips = new List<TrawellingTripDto>();

                foreach (var status in statusesResponse.Data)
                {
                    var trip = await MapStatusToTripDto(user, status);
                    if (trip != null)
                    {
                        optimizedTrips.Add(trip);
                    }
                }

                return new TrawellingTripsResponse
                {
                    Data = optimizedTrips,
                    Links = statusesResponse.Links,
                    Meta = statusesResponse.Meta,
                    HasMorePages = statusesResponse.Links?.Next != null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting optimized trips for user {UserId}", user.Id);
                return null;
            }
        }

        public async Task<TrawellingStationData> GetStationDataAsync(User user, int stationId)
        {
            try
            {
                // First check if we have the station data in our database
                var cachedStation = await _dbContext.TrawellingStations
                    .FirstOrDefaultAsync(ts => ts.Id == stationId);

                if (cachedStation != null)
                {
                    return new TrawellingStationData
                    {
                        Id = cachedStation.Id,
                        Name = cachedStation.Name,
                        Latitude = cachedStation.Latitude,
                        Longitude = cachedStation.Longitude,
                        Ibnr = cachedStation.Ibnr,
                        RilIdentifier = cachedStation.RilIdentifier
                    };
                }

                // If not cached, fetch from API
                if (!await EnsureValidTokenAsync(user))
                    return null;

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {user.TrawellingAccessToken}");

                var response = await _httpClient.GetAsync($"{_baseUrl}/stations/{stationId}");
                
                // Update rate limit tracking
                UpdateRateLimitInfo(response);

                // Check for rate limiting and implement backoff
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("Rate limit hit when fetching station {StationId}. Implementing backoff.", stationId);
                    await Task.Delay(TimeSpan.FromMinutes(1));
                    response = await _httpClient.GetAsync($"{_baseUrl}/stations/{stationId}");
                    UpdateRateLimitInfo(response);
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get station {StationId}. Status: {StatusCode}", stationId, response.StatusCode);
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var stationResponse = JsonConvert.DeserializeObject<TrawellingStationResponse>(responseContent);

                var stationData = stationResponse?.Data;
                if (stationData != null)
                {
                    // Store in database for future use
                    var dbStation = new OVDB_database.Models.TrawellingStation
                    {
                        Id = stationData.Id,
                        Name = stationData.Name,
                        Latitude = stationData.Latitude,
                        Longitude = stationData.Longitude,
                        Ibnr = stationData.Ibnr,
                        RilIdentifier = stationData.RilIdentifier,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _dbContext.TrawellingStations.Add(dbStation);
                    await _dbContext.SaveChangesAsync();

                    _logger.LogInformation("Cached station data for station {StationId}: {StationName}", stationId, stationData.Name);
                }

                return stationData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting station data for station {StationId}", stationId);
                return null;
            }
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

        private async Task<bool> EnsureValidTokenAsync(User user)
        {
            if (HasValidTokens(user))
                return true;

            if (!string.IsNullOrEmpty(user.TrawellingRefreshToken))
            {
                return await RefreshTokensAsync(user);
            }

            _logger.LogWarning("User {UserId} has no valid Träwelling tokens", user.Id);
            return false;
        }

        private async Task<Route> FindOrCreateRouteAsync(TrawellingStatus status)
        {
            try
            {
                if (status.Train?.Origin == null || status.Train?.Destination == null)
                {
                    _logger.LogWarning("Status {StatusId} missing origin or destination data", status.Id);
                    return null;
                }

                var originName = status.Train.Origin.Name;
                var destinationName = status.Train.Destination.Name;
                var lineName = status.Train.LineName ?? $"{status.Train.Category} {status.Train.Number}";

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
                    FirstDateTime = status.CreatedAt,
                    Share = Guid.NewGuid(),
                    CalculatedDistance = status.Train.Distance > 0 ? status.Train.Distance / 1000.0 : 0 // Convert meters to km
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
                if (status.Train?.Origin == null || status.Train?.Destination == null)
                    return false;

                var originName = status.Train.Origin.Name;
                var destinationName = status.Train.Destination.Name;
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

                if (!bestMatch.StartTime.HasValue && status.Train.Origin.Departure.HasValue)
                {
                    bestMatch.StartTime = status.Train.Origin.Departure.Value;
                    updated = true;
                }

                if (!bestMatch.EndTime.HasValue && status.Train.Destination.Arrival.HasValue)
                {
                    bestMatch.EndTime = status.Train.Destination.Arrival.Value;
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

                // Add Träwelling metadata properties if they don't exist
                var existingKeys = bestMatch.RouteInstanceProperties?.Select(p => p.Key).ToHashSet() ?? new HashSet<string>();
                var newProperties = new List<RouteInstanceProperty>();

                if (!existingKeys.Contains("traewelling_line") && !string.IsNullOrEmpty(status.Train.LineName))
                {
                    newProperties.Add(new RouteInstanceProperty
                    {
                        RouteInstanceId = bestMatch.RouteInstanceId,
                        Key = "traewelling_line",
                        Value = status.Train.LineName
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
                    // Update timing data if the RouteInstance doesn't have it
                    if (statusData.Train?.Origin?.Departure.HasValue == true)
                    {
                        routeInstance.StartTime = statusData.Train.ManualDeparture?? statusData.Train.Origin.Departure;
                    }

                    if (statusData.Train?.Destination?.Arrival.HasValue == true)
                    {
                        routeInstance.EndTime = statusData.Train.ManualArrival ?? statusData.Train.Destination.Arrival;
                    }

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

        private async Task<TrawellingStatus> GetStatusAsync(User user, int statusId)
        {
            try
            {
               
                if(_memoryCache.TryGetValue($"TraewellingStatus|{statusId}", out TrawellingStatus cachedStatus))
                {
                    return cachedStatus;
                }


                // Cache miss or expired - fetch from API
                if (!await EnsureValidTokenAsync(user))
                    return null;

                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", user.TrawellingAccessToken);

                var response = await _httpClient.GetAsync($"{_baseUrl}/status/{statusId}");

                // Update rate limit tracking
                UpdateRateLimitInfo(response);

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var statusResponse = JsonConvert.DeserializeObject<TrawellingStatusResponse>(content);

                var status = statusResponse?.Data;
                if (status != null)
                {
                    _memoryCache.Set("TraewellingStatus|" + status.Id, status, TimeSpan.FromMinutes(30));
                }

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Träwelling status {StatusId}", statusId);
                return null;
            }
        }

        private async Task<TrawellingTripDto> MapStatusToTripDto(User user, TrawellingStatus status)
        {
            try
            {
                if (status.Train?.Origin == null || status.Train?.Destination == null)
                    return null;

                var origin = await MapStopoverToDto(user, status.Train.Origin, status.Train.ManualDeparture);
                var destination = await MapStopoverToDto(user, status.Train.Destination, status.Train.ManualArrival);

                return new TrawellingTripDto
                {
                    Id = status.Id,
                    Body = status.Body,
                    Business = status.Business,
                    Visibility = status.Visibility,
                    CreatedAt = status.CreatedAt,
                    Transport = new TrawellingTransportDto
                    {
                        Category = GetTransportCategoryDisplayName(status.Train.Category),
                        Number = status.Train.Number,
                        LineName = status.Train.LineName,
                        JourneyNumber = status.Train.JourneyNumber,
                        Distance = status.Train.Distance,
                        Duration = status.Train.Duration,
                        Origin = origin,
                        Destination = destination,
                        Operator = status.Train.Operator != null ? new TrawellingOperatorDto
                        {
                            Name = status.Train.Operator.Name,
                            Identifier = status.Train.Operator.Identifier
                        } : null
                    },
                    UserDetails = new TrawellingLightUserDto
                    {
                        Id = status.UserDetails.Id,
                        DisplayName = status.UserDetails.DisplayName,
                        Username = status.UserDetails.Username,
                        ProfilePicture = status.UserDetails.ProfilePicture
                    },
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

        private async Task<TrawellingStopoverDto> MapStopoverToDto(User user, TrawellingStopover stopover, DateTime? manualTime)
        {
            try
            {
                // Get station data with coordinates for timezone conversion
                var stationData = await GetStationDataAsync(user, stopover.Id);
                
                // Determine the best real times (manual times take precedence)
                DateTime? realArrival = null;
                DateTime? realDeparture = null;

                if (stopover.Arrival.HasValue)
                {
                    realArrival = stopover.ArrivalReal ?? stopover.Arrival;
                }
                
                if (stopover.Departure.HasValue)
                {
                    // Use manual departure time if this is the origin stop and manual time is provided
                    realDeparture = (manualTime.HasValue && stopover.Departure.HasValue) ? manualTime : (stopover.DepartureReal ?? stopover.Departure);
                }
                
                // For destination, use manual arrival time if provided
                if (manualTime.HasValue && !stopover.Departure.HasValue && stopover.Arrival.HasValue)
                {
                    realArrival = manualTime;
                }

                // Convert UTC times to local timezone if we have coordinates
                DateTime? localArrivalScheduled = null;
                DateTime? localDepartureScheduled = null;
                DateTime? localArrivalReal = null;
                DateTime? localDepartureReal = null;

                if (stationData?.Latitude.HasValue == true && stationData?.Longitude.HasValue == true)
                {
                    var coordinates = $"{stationData.Latitude},{stationData.Longitude}";
                    
                    if (stopover.ArrivalPlanned.HasValue)
                        localArrivalScheduled = await _timezoneService.ConvertUtcToLocalTimeAsync(stopover.ArrivalPlanned.Value, coordinates);
                    
                    if (stopover.DeparturePlanned.HasValue)
                        localDepartureScheduled = await _timezoneService.ConvertUtcToLocalTimeAsync(stopover.DeparturePlanned.Value, coordinates);
                    
                    if (realArrival.HasValue)
                        localArrivalReal = await _timezoneService.ConvertUtcToLocalTimeAsync(realArrival.Value, coordinates);
                    
                    if (realDeparture.HasValue)
                        localDepartureReal = await _timezoneService.ConvertUtcToLocalTimeAsync(realDeparture.Value, coordinates);
                }
                else
                {
                    // Fallback to UTC times if no coordinates available
                    localArrivalScheduled = stopover.ArrivalPlanned;
                    localDepartureScheduled = stopover.DeparturePlanned;
                    localArrivalReal = realArrival;
                    localDepartureReal = realDeparture;
                    
                    _logger.LogWarning("No coordinates available for station {StationId}, using UTC times", stopover.Id);
                }

                return new TrawellingStopoverDto
                {
                    Id = stopover.Id,
                    Name = stopover.Name,
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
                _logger.LogError(ex, "Error mapping stopover {StopoverId} to DTO", stopover.Id);
                // Return basic DTO without timezone conversion on error
                return new TrawellingStopoverDto
                {
                    Id = stopover.Id,
                    Name = stopover.Name,
                    ArrivalScheduled = stopover.ArrivalPlanned,
                    DepartureScheduled = stopover.DeparturePlanned,
                    ArrivalReal = stopover.ArrivalReal ?? stopover.Arrival,
                    DepartureReal = stopover.DepartureReal ?? stopover.Departure,
                    IsArrivalDelayed = stopover.IsArrivalDelayed,
                    IsDepartureDelayed = stopover.IsDepartureDelayed,
                    Cancelled = stopover.Cancelled
                };
            }
        }

        private string GetTransportCategoryDisplayName(TrawellingHafasTravelType category)
        {
            return category switch
            {
                TrawellingHafasTravelType.NationalExpress => "High-Speed Train",
                TrawellingHafasTravelType.National => "Intercity Train", 
                TrawellingHafasTravelType.RegionalExp => "Regional Express",
                TrawellingHafasTravelType.Regional => "Regional Train",
                TrawellingHafasTravelType.Suburban => "Suburban Train",
                TrawellingHafasTravelType.Bus => "Bus",
                TrawellingHafasTravelType.Ferry => "Ferry",
                TrawellingHafasTravelType.Subway => "Subway",
                TrawellingHafasTravelType.Tram => "Tram",
                TrawellingHafasTravelType.Taxi => "Taxi",
                TrawellingHafasTravelType.Plane => "Plane",
                _ => category.ToString()
            };
        }

        private void UpdateRateLimitInfo(HttpResponseMessage response)
        {
            try
            {
                if (response.Headers.TryGetValues("x-ratelimit-limit", out var limitValues))
                {
                    if (int.TryParse(limitValues.FirstOrDefault(), out var limit))
                    {
                        _rateLimitLimit = limit;
                    }
                }

                if (response.Headers.TryGetValues("x-ratelimit-remaining", out var remainingValues))
                {
                    if (int.TryParse(remainingValues.FirstOrDefault(), out var remaining))
                    {
                        _rateLimitRemaining = remaining;
                    }
                }

                _rateLimitUpdated = DateTime.UtcNow;

                if (_rateLimitLimit.HasValue && _rateLimitRemaining.HasValue)
                {
                    _logger.LogDebug("Träwelling API rate limit: {Remaining}/{Limit} remaining",
                        _rateLimitRemaining.Value, _rateLimitLimit.Value);

                    if (_rateLimitRemaining.Value < 10)
                    {
                        _logger.LogWarning("Träwelling API rate limit is low: {Remaining}/{Limit} remaining",
                            _rateLimitRemaining.Value, _rateLimitLimit.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error parsing rate limit headers");
            }
        }
    }
}