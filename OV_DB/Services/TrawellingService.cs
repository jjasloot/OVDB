using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OV_DB.Models;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Services
{
    public class TrawellingService : ITrawellingService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly OVDBDatabaseContext _dbContext;
        private readonly ILogger<TrawellingService> _logger;
        private readonly string _baseUrl;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _redirectUri;
        private readonly string _authorizeUrl;
        private readonly string _tokenUrl;

        // Simple in-memory cache for OAuth states - in production, use Redis or database
        private static readonly Dictionary<string, (int UserId, DateTime Expiry)> _oauthStates = new();
        private static readonly object _statelock = new object();

        public TrawellingService(HttpClient httpClient, IConfiguration configuration, 
            OVDBDatabaseContext dbContext, ILogger<TrawellingService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _dbContext = dbContext;
            _logger = logger;
            
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

        public bool ValidateAndConsumeState(string state, int userId)
        {
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

                    // Check if user matches
                    if (stateInfo.UserId != userId)
                    {
                        _logger.LogWarning("OAuth state {State} user mismatch. Expected: {ExpectedUserId}, Actual: {ActualUserId}", 
                            state, stateInfo.UserId, userId);
                        return false;
                    }

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

        public async Task<TrawellingUserData> GetUserInfoAsync(User user)
        {
            try
            {
                if (!await EnsureValidTokenAsync(user))
                    return null;

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {user.TrawellingAccessToken}");

                var response = await _httpClient.GetAsync($"{_baseUrl}/auth/user");
                
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

                // Get user's statuses from Träwelling
                var response = await _httpClient.GetAsync($"{_baseUrl}/dashboard?page={page}");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get statuses for user {UserId}. Status: {StatusCode}", 
                        user.Id, response.StatusCode);
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var statusesResponse = JsonConvert.DeserializeObject<TrawellingStatusesResponse>(responseContent);

                if (statusesResponse?.Data != null)
                {
                    // Filter out statuses that are already imported
                    var existingTrawellingIds = await _dbContext.RouteInstances
                        .Where(ri => ri.TrawellingStatusId.HasValue)
                        .Select(ri => ri.TrawellingStatusId.Value)
                        .ToListAsync();

                    statusesResponse.Data = statusesResponse.Data
                        .Where(status => !existingTrawellingIds.Contains(status.Id))
                        .ToList();
                }

                return statusesResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unimported statuses for user {UserId}", user.Id);
                return null;
            }
        }

        public async Task<RouteInstance> ImportStatusAsync(User user, int statusId, bool importMetadata = true, bool importTags = true)
        {
            try
            {
                if (!await EnsureValidTokenAsync(user))
                    return null;

                // Check if already imported
                var existing = await _dbContext.RouteInstances
                    .FirstOrDefaultAsync(ri => ri.TrawellingStatusId == statusId);
                if (existing != null)
                {
                    _logger.LogWarning("Status {StatusId} already imported as RouteInstance {RouteInstanceId}", 
                        statusId, existing.RouteInstanceId);
                    return existing;
                }

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {user.TrawellingAccessToken}");

                // Get status details from Träwelling
                var response = await _httpClient.GetAsync($"{_baseUrl}/statuses/{statusId}");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get status {StatusId} for import. Status: {StatusCode}", 
                        statusId, response.StatusCode);
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var statusData = JsonConvert.DeserializeObject<TrawellingStatusResponse>(responseContent);
                var status = statusData?.Data;

                if (status == null)
                {
                    _logger.LogError("No status data found for status {StatusId}", statusId);
                    return null;
                }

                // Create or find route based on Träwelling data
                var route = await FindOrCreateRouteAsync(status);
                if (route == null)
                {
                    _logger.LogError("Failed to find or create route for status {StatusId}", statusId);
                    return null;
                }

                // Create RouteInstance
                var routeInstance = new RouteInstance
                {
                    RouteId = route.RouteId,
                    Date = status.CreatedAt.Date,
                    StartTime = status.Train?.Origin?.Departure,
                    EndTime = status.Train?.Destination?.Arrival,
                    TrawellingStatusId = statusId
                };

                // Calculate duration if we have both start and end times
                if (routeInstance.StartTime.HasValue && routeInstance.EndTime.HasValue)
                {
                    var duration = routeInstance.EndTime.Value - routeInstance.StartTime.Value;
                    routeInstance.DurationHours = duration.TotalHours;
                }

                _dbContext.RouteInstances.Add(routeInstance);
                await _dbContext.SaveChangesAsync();

                // Add properties if requested
                if (importMetadata && !string.IsNullOrEmpty(status.Body))
                {
                    var descriptionProperty = new RouteInstanceProperty
                    {
                        RouteInstanceId = routeInstance.RouteInstanceId,
                        Key = "traewelling_description",
                        Value = status.Body
                    };
                    _dbContext.RouteInstanceProperties.Add(descriptionProperty);
                }

                // Add Träwelling metadata properties
                var trawellingProperties = new List<RouteInstanceProperty>();

                if (status.Train != null)
                {
                    if (!string.IsNullOrEmpty(status.Train.LineName))
                    {
                        trawellingProperties.Add(new RouteInstanceProperty
                        {
                            RouteInstanceId = routeInstance.RouteInstanceId,
                            Key = "traewelling_line",
                            Value = status.Train.LineName
                        });
                    }

                    if (!string.IsNullOrEmpty(status.Train.Category))
                    {
                        trawellingProperties.Add(new RouteInstanceProperty
                        {
                            RouteInstanceId = routeInstance.RouteInstanceId,
                            Key = "traewelling_category",
                            Value = status.Train.Category
                        });
                    }

                    if (status.Train.Distance > 0)
                    {
                        trawellingProperties.Add(new RouteInstanceProperty
                        {
                            RouteInstanceId = routeInstance.RouteInstanceId,
                            Key = "traewelling_distance",
                            Value = status.Train.Distance.ToString()
                        });
                    }

                    if (status.Train.Duration > 0)
                    {
                        trawellingProperties.Add(new RouteInstanceProperty
                        {
                            RouteInstanceId = routeInstance.RouteInstanceId,
                            Key = "traewelling_duration",
                            Value = status.Train.Duration.ToString()
                        });
                    }
                }

                // Add source property
                trawellingProperties.Add(new RouteInstanceProperty
                {
                    RouteInstanceId = routeInstance.RouteInstanceId,
                    Key = "source",
                    Value = "traewelling"
                });

                _dbContext.RouteInstanceProperties.AddRange(trawellingProperties);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Successfully imported Träwelling status {StatusId} as RouteInstance {RouteInstanceId}", 
                    statusId, routeInstance.RouteInstanceId);

                return routeInstance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing status {StatusId} for user {UserId}", statusId, user.Id);
                return null;
            }
        }

        public async Task<int> ProcessBacklogAsync(User user, int maxPages = 10)
        {
            int processedCount = 0;
            int updatedCount = 0;
            
            try
            {
                _logger.LogInformation("Starting backlog processing for user {UserId}, maxPages: {MaxPages}", user.Id, maxPages);

                for (int page = 1; page <= maxPages; page++)
                {
                    _logger.LogDebug("Processing page {Page} of {MaxPages} for user {UserId}", page, maxPages, user.Id);
                    
                    var statusesResponse = await GetUnimportedStatusesAsync(user, page);
                    
                    if (statusesResponse?.Data == null || !statusesResponse.Data.Any())
                    {
                        _logger.LogInformation("No more data found at page {Page}, stopping backlog processing", page);
                        break;
                    }

                    foreach (var status in statusesResponse.Data)
                    {
                        try
                        {
                            // First try to import as new RouteInstance
                            var imported = await ImportStatusAsync(user, status.Id, true, true);
                            if (imported != null)
                            {
                                processedCount++;
                                _logger.LogInformation("Imported status {StatusId} as new RouteInstance {RouteInstanceId}", 
                                    status.Id, imported.RouteInstanceId);
                                continue;
                            }

                            // If import failed, try to update existing RouteInstance with timing data
                            var updated = await UpdateExistingRouteInstanceWithTrawellingDataAsync(user, status);
                            if (updated)
                            {
                                updatedCount++;
                                _logger.LogInformation("Updated existing RouteInstance with data from status {StatusId}", status.Id);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing status {StatusId} in backlog", status.Id);
                            // Continue with next status rather than failing entire backlog
                        }
                    }

                    // Add delay to avoid rate limiting
                    await Task.Delay(2000); // Increased delay for better rate limiting compliance
                    
                    _logger.LogDebug("Completed page {Page}, imported: {ImportedCount}, updated: {UpdatedCount}", 
                        page, processedCount, updatedCount);
                }

                _logger.LogInformation("Backlog processing completed for user {UserId}. Imported: {ImportedCount}, Updated: {UpdatedCount}", 
                    user.Id, processedCount, updatedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing backlog for user {UserId}", user.Id);
            }

            return processedCount + updatedCount;
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
                    .Where(ri => ri.Date.Date == date.Date);

                // Filter by user - assuming there's a relationship or we need to add one
                // For now, we'll search all RouteInstances since there's no user relationship in the model
                // In a real implementation, you might want to add a UserId to RouteInstance or filter differently

                if (!string.IsNullOrWhiteSpace(searchQuery))
                {
                    query = query.Where(ri => ri.Route.Name.Contains(searchQuery) ||
                                            ri.Route.From.Contains(searchQuery) ||
                                            ri.Route.To.Contains(searchQuery));
                }

                var routeInstances = await query
                    .OrderByDescending(ri => ri.StartTime ?? ri.Date)
                    .Take(20) // Limit to 20 results
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
                        routeInstance.StartTime = statusData.Train.Origin.Departure;
                    }

                    if (statusData.Train?.Destination?.Arrival.HasValue == true)
                    {
                        routeInstance.EndTime = statusData.Train.Destination.Arrival;
                    }

                    if (routeInstance.StartTime.HasValue && routeInstance.EndTime.HasValue)
                    {
                        routeInstance.DurationHours = (routeInstance.EndTime.Value - routeInstance.StartTime.Value).TotalHours;
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
                if (!await EnsureValidTokenAsync(user))
                    return null;

                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", user.TrawellingAccessToken);

                var response = await _httpClient.GetAsync($"{_baseUrl}/statuses/{statusId}");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var statusResponse = JsonConvert.DeserializeObject<TrawellingStatusResponse>(content);

                return statusResponse?.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Träwelling status {StatusId}", statusId);
                return null;
            }
        }
    }
}