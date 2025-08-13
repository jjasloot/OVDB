using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OV_DB.Models;
using OVDB_database.Models;

namespace OV_DB.Services
{
    public interface ITrawellingService
    {
        /// <summary>
        /// Generate OAuth2 authorization URL for user to connect their Träwelling account
        /// </summary>
        /// <param name="userId">OVDB User ID</param>
        /// <param name="state">OAuth state parameter for security</param>
        /// <returns>Authorization URL</returns>
        string GetAuthorizationUrl(int userId, string state);

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
        /// Refresh expired OAuth tokens
        /// </summary>
        /// <param name="user">User with refresh token</param>
        /// <returns>Success status</returns>
        Task<bool> RefreshTokensAsync(User user);

        /// <summary>
        /// Get authenticated user information from Träwelling
        /// </summary>
        /// <param name="user">User with valid tokens</param>
        /// <returns>Träwelling user data</returns>
        Task<TrawellingUserAuthData> GetUserInfoAsync(User user);

        /// <summary>
        /// Get user's statuses/check-ins from Träwelling that haven't been imported to OVDB or ignored
        /// </summary>
        /// <param name="user">User with valid tokens</param>
        /// <param name="page">Page number for pagination</param>
        /// <returns>List of Träwelling statuses</returns>
        Task<TrawellingStatusesResponse> GetUnimportedStatusesAsync(User user, int page = 1);

        /// <summary>
        /// Ignore a specific Träwelling status so it doesn't appear in unimported list
        /// </summary>
        /// <param name="user">User ignoring the status</param>
        /// <param name="statusId">Träwelling status ID to ignore</param>
        /// <returns>Success status</returns>
        Task<bool> IgnoreStatusAsync(User user, int statusId);

        /// <summary>
        /// Check if user has valid Träwelling tokens
        /// </summary>
        /// <param name="user">User to check</param>
        /// <returns>True if user has valid tokens</returns>
        bool HasValidTokens(User user);

        /// <summary>
        /// Get existing RouteInstances for a specific date and optionally filter by route name
        /// </summary>
        /// <param name="user">User to search RouteInstances for</param>
        /// <param name="date">Date to search for</param>
        /// <param name="searchQuery">Optional search query to filter by route name</param>
        /// <returns>List of matching RouteInstances</returns>
        Task<List<RouteInstance>> GetRouteInstancesByDateAsync(User user, DateTime date, string searchQuery = null);

        /// <summary>
        /// Link a Träwelling status to an existing RouteInstance
        /// </summary>
        /// <param name="user">User performing the link</param>
        /// <param name="statusId">Träwelling status ID</param>
        /// <param name="routeInstanceId">Existing OVDB RouteInstance ID</param>
        /// <returns>Updated RouteInstance or null if failed</returns>
        Task<RouteInstance> LinkStatusToRouteInstanceAsync(User user, int statusId, int routeInstanceId);
    }
}