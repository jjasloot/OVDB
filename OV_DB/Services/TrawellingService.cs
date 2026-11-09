using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using OV_DB.Hubs;
using OV_DB.Models;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OV_DB.Services
{
    public class TrawellingService : ITrawellingService
    {
        private readonly ITraewellingApiClient _api;
        private readonly TraewellingTripMapper _mapper;
        private readonly ITimezoneService _timezoneService;
        private readonly OVDBDatabaseContext _dbContext;
        private readonly ILogger<TrawellingService> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly IHubContext<TraewellingHub> _traewellingHubContext;

        // Matches the REST API's Newtonsoft configuration so hub payloads have the exact
        // same shape (camelCase, string enums via the DTO attributes) as the list endpoint
        private static readonly JsonSerializerSettings ApiJsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        };

        // Per user: a status can be private, and two OVDB accounts may read the same one.
        private static string TripCacheKey(int userId, int statusId) => $"TrawellingTrip|{userId}|{statusId}";

        public TrawellingService(ITraewellingApiClient api, TraewellingTripMapper mapper, ITimezoneService timezoneService,
            OVDBDatabaseContext dbContext, ILogger<TrawellingService> logger, IMemoryCache memoryCache,
            IHubContext<TraewellingHub> traewellingHubContext)
        {
            _api = api;
            _mapper = mapper;
            _timezoneService = timezoneService;
            _dbContext = dbContext;
            _logger = logger;
            _memoryCache = memoryCache;
            _traewellingHubContext = traewellingHubContext;
        }

        private const int SweepMaxPages = 10;

        // Safety valve for the full walk rather than a depth to tune: a rerun starts at page 1
        // again, so hitting this is not something the user can page past.
        private const int FullSweepMaxPages = 1000;

        private static readonly TimeSpan SweepStaleness = TimeSpan.FromHours(1);

        public async Task<TrawellingSweepResult> SweepInboxAsync(User user, TrawellingSweepMode mode = TrawellingSweepMode.WhenStale, IProgress<TrawellingSweepProgress> progress = null, CancellationToken cancellationToken = default)
        {
            var added = 0;
            var pagesRead = 0;
            try
            {
                if (mode == TrawellingSweepMode.WhenStale && user.TrawellingLastSweepAt.HasValue &&
                    DateTime.UtcNow - user.TrawellingLastSweepAt.Value < SweepStaleness)
                {
                    return new TrawellingSweepResult(true, 0, 0, false);
                }

                if (!await _api.EnsureValidTokenAsync(user))
                    return new TrawellingSweepResult(false, 0, 0, false);

                // Ensure we have the username for this user
                if (string.IsNullOrEmpty(user.TrawellingUsername))
                {
                    var userInfo = await _api.GetUserInfoAsync(user);
                    if (userInfo == null)
                    {
                        _logger.LogError("Could not fetch Träwelling username for user {UserId}", user.Id);
                        return new TrawellingSweepResult(false, 0, 0, false);
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
                var maxPages = mode == TrawellingSweepMode.Full ? FullSweepMaxPages : SweepMaxPages;
                var reachedEnd = false;

                for (var page = 1; page <= maxPages; page++)
                {
                    var response = await _api.GetAsync(user, $"user/{user.TrawellingUsername}/statuses?page={page}", cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("Inbox sweep failed for user {UserId}. Status: {StatusCode}", user.Id, response.StatusCode);
                        return new TrawellingSweepResult(false, added, pagesRead, false);
                    }

                    pagesRead++;
                    var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    var statusesResponse = JsonConvert.DeserializeObject<TrawellingStatusesResponse>(responseContent);
                    if (statusesResponse?.Data == null || statusesResponse.Data.Count == 0)
                    {
                        reachedEnd = true;
                        break;
                    }

                    var newOnPage = 0;
                    foreach (var status in statusesResponse.Data)
                    {
                        seenIds.Add(status.Id);
                        var departure = TraewellingTripMapper.GetStatusDeparture(status);
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

                    // Keep what this page found before asking for the next one, so a walk that is
                    // cut short still leaves the inbox fuller than it was.
                    if (newOnPage > 0)
                        await _dbContext.SaveChangesAsync(cancellationToken);

                    progress?.Report(new TrawellingSweepProgress(pagesRead, added));

                    if (string.IsNullOrEmpty(statusesResponse.Links?.Next))
                    {
                        reachedEnd = true;
                        break;
                    }

                    // Statuses are ordered newest-first, so a page without anything new only
                    // means everything older is known too while nothing was ever missed.
                    if (newOnPage == 0 && mode != TrawellingSweepMode.Full)
                        break;
                }

                if (mode == TrawellingSweepMode.Full && !reachedEnd)
                    _logger.LogWarning("Full inbox sweep for user {UserId} stopped at the {MaxPages}-page limit", user.Id, FullSweepMaxPages);

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
                return new TrawellingSweepResult(true, added, pagesRead, reachedEnd);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sweeping Träwelling inbox for user {UserId}", user.Id);
                return new TrawellingSweepResult(false, added, pagesRead, false);
            }
        }

        public async Task<TrawellingTripsResponse> GetOptimizedTripsAsync(User user, int page = 1, bool refresh = false, CancellationToken cancellationToken = default)
        {
            try
            {
                // The inbox is the source of the list; the statuses API is only touched by the
                // sweep (skipped when fresh, forced by the frontend's refresh action). A failed
                // sweep still returns the current — possibly stale — inbox contents.
                await SweepInboxAsync(user, refresh ? TrawellingSweepMode.Force : TrawellingSweepMode.WhenStale, cancellationToken: cancellationToken);

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
                    var cacheKey = TripCacheKey(user.Id, row.TrawellingStatusId);
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

                    var trip = status != null ? await _mapper.MapStatusToTripDtoAsync(status) : null;
                    if (trip == null)
                    {
                        // Early webhook deliveries were stored without the stopovers' station
                        // objects, which the mapping requires; refetch the complete status once
                        // and repair the stored payload.
                        var (fullJson, fullStatus) = await _api.FetchStatusRawAsync(user, row.TrawellingStatusId);
                        if (fullJson != null)
                        {
                            row.PayloadJson = fullJson;
                            row.DepartureAt = TraewellingTripMapper.GetStatusDeparture(fullStatus);
                            row.LastEventAt = DateTime.UtcNow;
                            await _dbContext.SaveChangesAsync();
                            trip = await _mapper.MapStatusToTripDtoAsync(fullStatus);
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
                if (row.State == TrawellingInboxState.ChangedAfterImport && !TraewellingTripMapper.UpstreamDiffers(conflict))
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
                newTrip = await _mapper.MapStatusToTripDtoAsync(status);
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
            var trip = await _mapper.MapStatusToTripDtoAsync(status);
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
                // Not waiting on the rate limit: the stored payload is repaired when the list loads.
                var (fullJson, fullStatus) = await _api.FetchStatusRawAsync(user, statusId, waitIfLimited: false);
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
            _memoryCache.Remove(TripCacheKey(user.Id, statusId));
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
                    var trip = await _mapper.MapStatusToTripDtoAsync(status);
                    if (trip == null)
                        return;
                    _memoryCache.Set(TripCacheKey(user.Id, statusId), trip, TimeSpan.FromMinutes(30));
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

        private bool DismissedFingerprintMatches(TrawellingInboxStatus dismissedRow, TrawellingStatus incoming)
        {
            try
            {
                var dismissed = JsonConvert.DeserializeObject<TrawellingStatus>(dismissedRow.PayloadJson);
                return dismissed != null && TraewellingTripMapper.ConflictFingerprint(dismissed) == TraewellingTripMapper.ConflictFingerprint(incoming);
            }
            catch
            {
                return false;
            }
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
            return TraewellingTripMapper.UpstreamDiffers(instance, await _mapper.MapStatusToTripDtoAsync(status));
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
            row.DepartureAt = TraewellingTripMapper.GetStatusDeparture(status);
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

        public async Task<List<RouteInstance>> GetRouteInstancesByDateAsync(User user, DateTime date)
        {
            try
            {
                var query = _dbContext.RouteInstances
                    .Include(ri => ri.Route)
                    .Where(ri => !ri.TrawellingStatusId.HasValue)
                    .Where(ri => ri.Route.RouteMaps.Any(rm => rm.Map.UserId == user.Id))
                    .Where(ri => ri.Date.Date == date.Date);

                var routeInstances = await query
                    .OrderByDescending(ri => ri.StartTime ?? ri.Date)
                    .ThenByDescending(ri => ri.RouteInstanceId)
                    .ToListAsync();

                _logger.LogInformation("Found {Count} RouteInstances for date {Date}", routeInstances.Count, date.Date);

                return routeInstances;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching RouteInstances for date {Date}", date);
                return new List<RouteInstance>();
            }
        }

        public async Task<RouteInstance> LinkStatusToRouteInstanceAsync(User user, int statusId, int routeInstanceId)
        {
            try
            {
                // Check if the Träwelling status is already linked to another of this user's
                // RouteInstances (two OVDB accounts may share one Träwelling account)
                var existingLink = await _dbContext.RouteInstances
                    .Where(ri => ri.RouteInstanceMaps.Any(rim => rim.Map.UserId == user.Id) || ri.Route.RouteMaps.Any(rm => rm.Map.UserId == user.Id))
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
                    .Where(ri => ri.RouteInstanceMaps.Any(rim => rim.Map.UserId == user.Id) || ri.Route.RouteMaps.Any(rm => rm.Map.UserId == user.Id))
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

                if (_memoryCache.TryGetValue(TripCacheKey(user.Id, statusId), out TrawellingTripDto cachedStatus))
                {
                    return cachedStatus;
                }

                // Cache miss or expired - fetch from API
                var (_, status) = await _api.FetchStatusRawAsync(user, statusId);
                if (status == null)
                    return null;

                var mapped = await _mapper.MapStatusToTripDtoAsync(status);
                _memoryCache.Set(TripCacheKey(user.Id, statusId), mapped, TimeSpan.FromMinutes(30));
                return mapped;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Träwelling status {StatusId}", statusId);
                return null;
            }
        }

        private const int BackfillBatchSize = 100;

        public async Task<(int found, int updated, int failed)> BackfillScheduledTimesAsync(User user)
        {
            var found = 0;
            var updated = 0;
            var failed = 0;

            try
            {
                var candidates = _dbContext.RouteInstances
                    .Where(ri => ri.TrawellingStatusId.HasValue &&
                                 ri.StartTime.HasValue &&
                                 !ri.ScheduledStartTime.HasValue &&
                                 !ri.ScheduledTimesUnavailable)
                    .Where(ri => ri.RouteInstanceMaps.Any(rim => rim.Map.UserId == user.Id) || ri.Route.RouteMaps.Any(rm => rm.Map.UserId == user.Id));
                found = await candidates.CountAsync();
                // One API call per trip: a click does one batch rather than an unbounded request,
                // and the page shows "updated N of found" so the user can click again.
                var instances = await candidates.OrderByDescending(ri => ri.Date).Take(BackfillBatchSize).ToListAsync();
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

                        if (statusData.Transport.Origin.DepartureScheduled == null && statusData.Transport.Destination.ArrivalScheduled == null)
                        {
                            // Nothing to fill in, now or later: don't fetch this one again.
                            instance.ScheduledTimesUnavailable = true;
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
                }

                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled times backfill for user {UserId}", user.Id);
            }

            return (found, updated, failed);
        }

    }
}