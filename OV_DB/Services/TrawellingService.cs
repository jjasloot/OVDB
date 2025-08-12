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
                var statusResponse = JsonConvert.DeserializeObject<dynamic>(responseContent);
                var status = statusResponse?.data;

                if (status == null)
                {
                    _logger.LogError("No status data found for status {StatusId}", statusId);
                    return null;
                }

                // TODO: Implement route matching and RouteInstance creation
                // This would involve:
                // 1. Finding or creating a Route based on origin/destination
                // 2. Creating a RouteInstance with the trip data
                // 3. Setting properties if importMetadata/importTags is true

                _logger.LogInformation("Status import for {StatusId} not yet fully implemented", statusId);
                return null;
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
            
            try
            {
                for (int page = 1; page <= maxPages; page++)
                {
                    var statusesResponse = await GetUnimportedStatusesAsync(user, page);
                    
                    if (statusesResponse?.Data == null || !statusesResponse.Data.Any())
                        break;

                    foreach (var status in statusesResponse.Data)
                    {
                        var imported = await ImportStatusAsync(user, status.Id);
                        if (imported != null)
                        {
                            processedCount++;
                            _logger.LogInformation("Imported status {StatusId} as part of backlog processing", status.Id);
                        }
                    }

                    // Add delay to avoid rate limiting
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing backlog for user {UserId}", user.Id);
            }

            return processedCount;
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
    }
}