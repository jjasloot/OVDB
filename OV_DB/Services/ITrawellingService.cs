using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OV_DB.Models;
using OVDB_database.Models;

namespace OV_DB.Services
{
    /// <summary>
    /// How far back a sweep reads the Träwelling statuses listing.
    /// </summary>
    public enum TrawellingSweepMode
    {
        /// <summary>Skip entirely while the last sweep is still fresh.</summary>
        WhenStale,

        /// <summary>Sweep now, stopping at the first page that holds nothing new.</summary>
        Force,

        /// <summary>
        /// Walk the whole listing, past pages that hold nothing new. The only way to reach a
        /// check-in that was missed while newer ones were already imported.
        /// </summary>
        Full,
    }

    /// <param name="Success">False when the listing could not be read.</param>
    /// <param name="Added">Statuses newly added to the inbox.</param>
    /// <param name="PagesRead">Pages of the statuses listing that were fetched.</param>
    /// <param name="ReachedEnd">True when the walk ran out of pages rather than stopping at a cap.</param>
    public record TrawellingSweepResult(bool Success, int Added, int PagesRead, bool ReachedEnd);

    /// <summary>How far a sweep has got, reported after every page.</summary>
    public record TrawellingSweepProgress(int PagesRead, int Added);

    public interface ITrawellingService
    {

        /// <summary>
        /// Get optimized trip data for frontend with local timezone conversion, served from
        /// the local inbox (swept from the Träwelling API when stale or when forced)
        /// </summary>
        /// <param name="user">User with valid tokens</param>
        /// <param name="page">Page number for pagination</param>
        /// <param name="refresh">Force a sweep of the Träwelling API before reading the inbox</param>
        /// <returns>Optimized trip data with local timing</returns>
        Task<TrawellingTripsResponse> GetOptimizedTripsAsync(User user, int page = 1, bool refresh = false, System.Threading.CancellationToken cancellationToken = default);

        /// <summary>
        /// Reconcile the local inbox with the Träwelling statuses API: adds unknown statuses
        /// as pending, removes pending rows that were deleted upstream.
        /// </summary>
        /// <param name="user">User to sweep for</param>
        /// <param name="mode">How far back to read the listing</param>
        /// <param name="progress">Reported after every page; the only sign of life during a full walk</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<TrawellingSweepResult> SweepInboxAsync(User user, TrawellingSweepMode mode = TrawellingSweepMode.WhenStale, IProgress<TrawellingSweepProgress> progress = null, System.Threading.CancellationToken cancellationToken = default);

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
        /// Existing RouteInstances on a date that are not linked to Träwelling yet
        /// </summary>
        Task<List<RouteInstance>> GetRouteInstancesByDateAsync(User user, DateTime date);

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

    }
}