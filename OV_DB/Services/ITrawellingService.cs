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
        /// Get optimized trip data for frontend with local timezone conversion, served from
        /// the local inbox (swept from the Träwelling API when stale or when forced)
        /// </summary>
        /// <param name="user">User with valid tokens</param>
        /// <param name="page">Page number for pagination</param>
        /// <param name="refresh">Force a sweep of the Träwelling API before reading the inbox</param>
        /// <returns>Optimized trip data with local timing</returns>
        Task<TrawellingTripsResponse> GetOptimizedTripsAsync(User user, int page = 1, bool refresh = false);

        /// <summary>
        /// Reconcile the local inbox with the Träwelling statuses API: adds unknown statuses
        /// as pending, removes pending rows that were deleted upstream. Skipped when the last
        /// sweep is fresh, unless forced.
        /// </summary>
        /// <param name="user">User to sweep for</param>
        /// <param name="force">Sweep even when the last sweep is recent</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True when the sweep completed (or was fresh enough to skip)</returns>
        Task<bool> SweepInboxAsync(User user, bool force = false, System.Threading.CancellationToken cancellationToken = default);

        /// <summary>
        /// Remove a status from the inbox after it has been imported or ignored
        /// </summary>
        /// <param name="userId">OVDB user id</param>
        /// <param name="statusId">Träwelling status id</param>
        Task RemoveFromInboxAsync(int userId, int statusId);

        /// <summary>
        /// List imported trips whose Träwelling status was edited or deleted upstream,
        /// awaiting the user's decision
        /// </summary>
        /// <param name="user">User to list conflicts for</param>
        Task<List<TrawellingConflictDto>> GetConflictsAsync(User user);

        /// <summary>
        /// Apply the upstream time changes of a ChangedAfterImport conflict to the linked
        /// RouteInstance and resolve the conflict
        /// </summary>
        Task<bool> ApplyConflictTimesAsync(User user, int statusId);

        /// <summary>
        /// Unlink the RouteInstance and put the status back in the unimported list
        /// </summary>
        Task<bool> ReimportConflictAsync(User user, int statusId);

        /// <summary>
        /// Dismiss a conflict; a dismissed change won't re-flag unless the journey facts
        /// change again
        /// </summary>
        Task<bool> DismissConflictAsync(User user, int statusId);

        /// <summary>
        /// Delete the linked RouteInstance following an upstream deletion (DeletedUpstream)
        /// </summary>
        Task<bool> DeleteInstanceForConflictAsync(User user, int statusId);

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
        /// Apply a verified webhook delivery to the inbox: creates/updates pending statuses,
        /// flags upstream edits/deletes of imported trips, respects ignores. The caller has
        /// already verified the signature.
        /// </summary>
        /// <param name="user">User the webhook belongs to</param>
        /// <param name="payloadJson">Raw delivery body: {event, status}</param>
        Task ProcessWebhookEventAsync(User user, string payloadJson);

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

        /// <summary>
        /// Backfill scheduled (planned) departure and arrival times for existing trips imported from Träwelling
        /// </summary>
        /// <param name="user">User whose trips to backfill</param>
        /// <returns>Counts of (found, updated, failed) trips</returns>
        Task<(int found, int updated, int failed)> BackfillScheduledTimesAsync(User user);

        /// <summary>
        /// Fetch active warning/danger alerts from the Träwelling platform
        /// </summary>
        /// <param name="user">User with valid tokens</param>
        /// <returns>List of active warning or danger alerts</returns>
        Task<List<TrawellingAlert>> GetAlertsAsync(User user);
    }
}