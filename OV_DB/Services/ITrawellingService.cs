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
        bool ValidateAndConsumeState(string state, int userId);

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
        Task<TrawellingUserData> GetUserInfoAsync(User user);

        /// <summary>
        /// Get user's statuses/check-ins from Träwelling that haven't been imported to OVDB
        /// </summary>
        /// <param name="user">User with valid tokens</param>
        /// <param name="page">Page number for pagination</param>
        /// <returns>List of Träwelling statuses</returns>
        Task<TrawellingStatusesResponse> GetUnimportedStatusesAsync(User user, int page = 1);

        /// <summary>
        /// Import a specific Träwelling status as a RouteInstance in OVDB
        /// </summary>
        /// <param name="user">User importing the trip</param>
        /// <param name="statusId">Träwelling status ID</param>
        /// <param name="importMetadata">Whether to import metadata like body text</param>
        /// <param name="importTags">Whether to import tags</param>
        /// <returns>Created RouteInstance or null if failed</returns>
        Task<RouteInstance> ImportStatusAsync(User user, int statusId, bool importMetadata = true, bool importTags = true);

        /// <summary>
        /// Process backlog of user's Träwelling trips to update trip times in OVDB
        /// </summary>
        /// <param name="user">User to process backlog for</param>
        /// <param name="maxPages">Maximum pages to process</param>
        /// <returns>Number of trips processed</returns>
        Task<int> ProcessBacklogAsync(User user, int maxPages = 10);

        /// <summary>
        /// Check if user has valid Träwelling tokens
        /// </summary>
        /// <param name="user">User to check</param>
        /// <returns>True if user has valid tokens</returns>
        bool HasValidTokens(User user);
    }
}