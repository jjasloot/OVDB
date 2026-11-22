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
        private static string TripCacheKey(int userId, TraewellingStatusKey key) => $"TrawellingTrip|{userId}|{key}";

        /// <summary>Drops a cached trip under either form of its key: rows change key when their UUID is stamped.</summary>
        private void ForgetTrip(int userId, TraewellingStatusKey key)
        {
            _memoryCache.Remove(TripCacheKey(userId, key));
            if (key.Id.HasValue)
                _memoryCache.Remove(TripCacheKey(userId, new TraewellingStatusKey(key.Id, null)));
        }

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
                var known = new KnownStatuses();
                known.AddRange(await _dbContext.RouteInstances
                    .Where(ri => ri.TrawellingStatusId.HasValue || ri.TrawellingStatusUuid != null)
                    .Where(ri => ri.RouteInstanceMaps.Any(rim => rim.Map.UserId == user.Id)
                        || ri.Route.RouteMaps.Any(rm => rm.Map.UserId == user.Id))
                    .Select(ri => new TraewellingStatusKey(ri.TrawellingStatusId, ri.TrawellingStatusUuid))
                    .ToListAsync(cancellationToken));
                known.AddRange(await _dbContext.TrawellingIgnoredStatuses
                    .Where(tis => tis.UserId == user.Id)
                    .Select(tis => new TraewellingStatusKey(tis.TrawellingStatusId, tis.TrawellingStatusUuid))
                    .ToListAsync(cancellationToken));
                known.AddRange(await _dbContext.TrawellingInboxStatuses
                    .Where(s => s.UserId == user.Id)
                    .Select(s => new TraewellingStatusKey(s.TrawellingStatusId, s.TrawellingStatusUuid))
                    .ToListAsync(cancellationToken));

                var seen = new KnownStatuses();
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
                    var knownUuids = new Dictionary<int, string>();
                    foreach (var status in statusesResponse.Data)
                    {
                        var key = TraewellingStatusKey.Of(status);
                        if (key.IsEmpty)
                            continue;
                        seen.Add(key);
                        var isKnown = known.Contains(key);
                        if (isKnown && key.Id.HasValue && key.Uuid != null)
                            knownUuids[key.Id.Value] = key.Uuid;
                        var departure = TraewellingTripMapper.GetStatusDeparture(status);
                        if (departure.HasValue && (!oldestSweptDeparture.HasValue || departure < oldestSweptDeparture))
                            oldestSweptDeparture = departure;

                        if (isKnown)
                            continue;

                        _dbContext.TrawellingInboxStatuses.Add(new TrawellingInboxStatus
                        {
                            UserId = user.Id,
                            TrawellingStatusId = key.Id,
                            TrawellingStatusUuid = key.Uuid,
                            PayloadJson = JsonConvert.SerializeObject(status),
                            State = TrawellingInboxState.Pending,
                            Source = TrawellingInboxSource.Sweep,
                            DepartureAt = departure,
                            ReceivedAt = DateTime.UtcNow,
                            LastEventAt = DateTime.UtcNow,
                        });
                        known.Add(key);
                        newOnPage++;
                        added++;
                    }

                    // Statuses OVDB already has get their UUID on the way past, so a full resync
                    // leaves every import with both ids.
                    var stamped = await StampStatusUuidsAsync(user, knownUuids, cancellationToken);

                    // Keep what this page found before asking for the next one, so a walk that is
                    // cut short still leaves the inbox fuller than it was.
                    if (newOnPage > 0)
                        MarkReceived(user);
                    if (newOnPage > 0 || stamped)
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
                if (!seen.IsEmpty && oldestSweptDeparture.HasValue)
                {
                    // Compared in memory: a row may be known here by its number, its UUID or both.
                    var deletedUpstream = (await _dbContext.TrawellingInboxStatuses
                        .Where(s => s.UserId == user.Id
                            && s.State == TrawellingInboxState.Pending
                            && s.DepartureAt >= oldestSweptDeparture.Value)
                        .ToListAsync(cancellationToken))
                        .Where(s => !seen.Contains(TraewellingStatusKey.Of(s)))
                        .ToList();
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
                    .ThenByDescending(s => s.TrawellingStatusId)
                    .ThenByDescending(s => s.Id); // UUID-only rows have no number to order by

                var totalCount = await query.CountAsync();
                var rows = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var optimizedTrips = new List<TrawellingTripDto>();
                foreach (var row in rows)
                {
                    // Check if we have timezone-corrected trip cached
                    var rowKey = TraewellingStatusKey.Of(row);
                    var cacheKey = TripCacheKey(user.Id, rowKey);
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
                        var (fullJson, fullStatus) = await _api.FetchStatusRawAsync(user, rowKey);
                        if (fullJson != null)
                        {
                            row.PayloadJson = fullJson;
                            row.TrawellingStatusUuid ??= fullStatus?.Uuid;
                            row.DepartureAt = TraewellingTripMapper.GetStatusDeparture(fullStatus);
                            row.LastEventAt = DateTime.UtcNow;
                            await _dbContext.SaveChangesAsync();
                            trip = await _mapper.MapStatusToTripDtoAsync(fullStatus);
                        }
                    }

                    if (trip != null)
                    {
                        // The row's key, not the payload's: payloads from before UUIDs carry only the number.
                        trip.Id = rowKey.ToString();
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
        public async Task RemoveFromInboxAsync(int userId, TraewellingStatusKey key)
        {
            await _dbContext.TrawellingInboxStatuses
                .Where(s => s.UserId == userId)
                .WhereStatus(key)
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
            var instance = await FindImportedInstanceAsync(user, TraewellingStatusKey.Of(row));
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
                StatusId = TraewellingStatusKey.Of(row).ToString(),
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

        public async Task<bool> ApplyConflictTimesAsync(User user, TraewellingStatusKey key)
        {
            var row = await GetConflictRowAsync(user, key, TrawellingInboxState.ChangedAfterImport);
            var instance = row == null ? null : await FindImportedInstanceAsync(user, TraewellingStatusKey.Of(row));
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
            _logger.LogInformation("Applied upstream time changes of status {StatusId} to RouteInstance {RouteInstanceId}", key, instance.RouteInstanceId);
            return true;
        }

        public async Task<bool> ReimportConflictAsync(User user, TraewellingStatusKey key)
        {
            var row = await GetConflictRowAsync(user, key, TrawellingInboxState.ChangedAfterImport);
            var instance = row == null ? null : await FindImportedInstanceAsync(user, TraewellingStatusKey.Of(row));
            if (row == null || instance == null)
                return false;

            // Unlink and hand the status back to the unimported list for a fresh import
            instance.TrawellingStatusId = null;
            instance.TrawellingStatusUuid = null;
            row.State = TrawellingInboxState.Pending;
            row.LastEventAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Unlinked status {StatusId} from RouteInstance {RouteInstanceId} for re-import", key, instance.RouteInstanceId);
            return true;
        }

        public async Task<bool> DismissConflictAsync(User user, TraewellingStatusKey key)
        {
            var row = await _dbContext.TrawellingInboxStatuses
                .Where(s => s.UserId == user.Id).WhereStatus(key)
                    .FirstOrDefaultAsync(s => (s.State == TrawellingInboxState.ChangedAfterImport || s.State == TrawellingInboxState.DeletedUpstream));
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

        public async Task<bool> DeleteInstanceForConflictAsync(User user, TraewellingStatusKey key)
        {
            var row = await GetConflictRowAsync(user, key, TrawellingInboxState.DeletedUpstream);
            var instance = row == null ? null : await FindImportedInstanceAsync(user, TraewellingStatusKey.Of(row));
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
                instance.RouteInstanceId, hasSiblingInstances ? "" : " and its route " + instance.RouteId, key);
            return true;
        }

        private Task<TrawellingInboxStatus> GetConflictRowAsync(User user, TraewellingStatusKey key, TrawellingInboxState state)
            => _dbContext.TrawellingInboxStatuses
                .Where(s => s.UserId == user.Id && s.State == state)
                .WhereStatus(key)
                .FirstOrDefaultAsync();

        /// <summary>Gives this user's trips, inbox rows and ignored statuses their status UUID where still missing.</summary>
        private async Task<bool> StampStatusUuidsAsync(User user, Dictionary<int, string> uuids, CancellationToken cancellationToken)
        {
            if (uuids.Count == 0)
                return false;
            var ids = uuids.Keys.Select(id => (int?)id).ToList();
            var changed = false;
            foreach (var ri in await _dbContext.RouteInstances
                .Where(ri => ids.Contains(ri.TrawellingStatusId) && ri.TrawellingStatusUuid == null)
                .Where(ri => ri.RouteInstanceMaps.Any(rim => rim.Map.UserId == user.Id)
                    || ri.Route.RouteMaps.Any(rm => rm.Map.UserId == user.Id))
                .ToListAsync(cancellationToken))
            {
                ri.TrawellingStatusUuid = uuids[ri.TrawellingStatusId!.Value];
                changed = true;
            }
            foreach (var row in await _dbContext.TrawellingInboxStatuses
                .Where(s => s.UserId == user.Id && ids.Contains(s.TrawellingStatusId) && s.TrawellingStatusUuid == null)
                .ToListAsync(cancellationToken))
            {
                row.TrawellingStatusUuid = uuids[row.TrawellingStatusId!.Value];
                changed = true;
            }
            foreach (var ignored in await _dbContext.TrawellingIgnoredStatuses
                .Where(s => s.UserId == user.Id && ids.Contains(s.TrawellingStatusId) && s.TrawellingStatusUuid == null)
                .ToListAsync(cancellationToken))
            {
                ignored.TrawellingStatusUuid = uuids[ignored.TrawellingStatusId!.Value];
                changed = true;
            }
            return changed;
        }

        /// <summary>Records that Träwelling delivered something for this user (saved with the caller's changes).</summary>
        private void MarkReceived(User user)
        {
            if (_dbContext.Entry(user).State == EntityState.Detached)
                _dbContext.Users.Attach(user);
            user.TraewellingLastReceivedAt = DateTime.UtcNow;
        }

        private Task<RouteInstance> FindImportedInstanceAsync(User user, TraewellingStatusKey key)
            => _dbContext.RouteInstances
                .Include(ri => ri.Route)
                .WhereStatus(key)
                .Where(ri => ri.RouteInstanceMaps.Any(rim => rim.Map.UserId == user.Id)
                    || ri.Route.RouteMaps.Any(rm => rm.Map.UserId == user.Id))
                .FirstOrDefaultAsync();

        /// <summary>This user's inbox row for the status, if any.</summary>
        private Task<TrawellingInboxStatus> FindInboxRowAsync(User user, TraewellingStatusKey key)
            => _dbContext.TrawellingInboxStatuses
                .Where(s => s.UserId == user.Id)
                .WhereStatus(key)
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
            if (status == null || TraewellingStatusKey.Of(status).IsEmpty)
                return;
            var statusJson = statusToken.ToString(Formatting.None);

            // The delivery payload omits relations that happened not to be loaded upstream —
            // notably the stopovers' station objects, which the trip mapping requires. Treat
            // the delivery as a trigger and fetch the complete status; keep the delivery
            // payload only as a fallback when the fetch fails.
            if (eventType is "checkin_create" or "checkin_update")
            {
                // Not waiting on the rate limit: the stored payload is repaired when the list loads.
                var (fullJson, fullStatus) = await _api.FetchStatusRawAsync(user, TraewellingStatusKey.Of(status), waitIfLimited: false);
                if (fullJson != null)
                {
                    fullStatus.Uuid ??= status.Uuid;
                    if (fullStatus.Id == 0)
                        fullStatus.Id = status.Id;
                    statusJson = fullJson;
                    status = fullStatus;
                }
                else
                {
                    _logger.LogWarning("Could not fetch full status {StatusId} for user {UserId}, storing the webhook delivery payload", TraewellingStatusKey.Of(status), user.Id);
                }
            }

            var key = TraewellingStatusKey.Of(status);
            var imported = await _dbContext.RouteInstances
                .WhereStatus(key)
                .Where(ri => ri.RouteInstanceMaps.Any(rim => rim.Map.UserId == user.Id)
                    || ri.Route.RouteMaps.Any(rm => rm.Map.UserId == user.Id))
                .AnyAsync();
            var ignored = await _dbContext.TrawellingIgnoredStatuses
                .Where(tis => tis.UserId == user.Id)
                .WhereStatus(key)
                .AnyAsync();
            var inboxRow = await FindInboxRowAsync(user, key);
            // What the open list knows this status by, for the live events below.
            var listKey = inboxRow != null ? TraewellingStatusKey.Of(inboxRow) : key;
            var previousListKey = listKey;

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
                            if (!await UpstreamDiffersAsync(user, key, status))
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
                            conflictRow = UpsertInboxRow(inboxRow, user.Id, key, statusJson, status, TrawellingInboxState.ChangedAfterImport);
                        }
                        break;
                    }
                    // Pending or unknown: (re)store as pending — an update for a status we
                    // never saw doubles as the missed create
                    listKey = TraewellingStatusKey.Of(UpsertInboxRow(inboxRow, user.Id, key, statusJson, status, TrawellingInboxState.Pending));
                    pendingUpserted = true;
                    break;

                case "checkin_delete":
                    if (imported)
                    {
                        // OVDB is the archive: flag it, never auto-delete the RouteInstance
                        conflictRow = UpsertInboxRow(inboxRow, user.Id, key, statusJson, status, TrawellingInboxState.DeletedUpstream);
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
                                .Where(tis => tis.UserId == user.Id)
                                .WhereStatus(key)
                                .ToListAsync();
                            _dbContext.TrawellingIgnoredStatuses.RemoveRange(ignoreRows);
                        }
                    }
                    break;

                default:
                    return; // not subscribed to anything else; ignore defensively
            }

            MarkReceived(user);
            await _dbContext.SaveChangesAsync();
            // The 30-minute trip DTO cache would otherwise serve the pre-update payload
            ForgetTrip(user.Id, key);
            ForgetTrip(user.Id, listKey);
            _logger.LogInformation("Processed Träwelling webhook {Event} for status {StatusId}, user {UserId}", eventType, key, user.Id);

            // A row that just got its UUID changed key: take the card off under the old one first.
            if (pendingUpserted && inboxRow != null && previousListKey != listKey)
                await PublishPendingTripChangeAsync(user, previousListKey, status, false, true);
            await PublishPendingTripChangeAsync(user, listKey, status, pendingUpserted, pendingRemoved);
            await PublishConflictChangeAsync(user, conflictRow);
            if (conflictRemoved)
            {
                await PublishConflictRemovedAsync(user, listKey);
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
                _logger.LogError(ex, "Error publishing live Träwelling conflict for status {StatusId}, user {UserId}", TraewellingStatusKey.Of(conflictRow), user.Id);
            }
        }

        /// <summary>
        /// Takes a conflict card off the open pages once it has nothing left to report.
        /// </summary>
        private async Task PublishConflictRemovedAsync(User user, TraewellingStatusKey key)
        {
            try
            {
                await _traewellingHubContext.Clients.User(user.Id.ToString())
                    .SendAsync(TraewellingHub.ConflictRemovedMethod, key.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing live Träwelling conflict removal for status {StatusId}, user {UserId}", key, user.Id);
            }
        }

        /// <summary>
        /// Pushes a live update for the unimported-trips list to the owning user's open
        /// pages. Only pending-list changes are pushed; the conflict states get their UI
        /// in a later phase. Delivery failures are logged and ignored — the list is
        /// DB-backed, so a missed push only means the change appears on the next load.
        /// </summary>
        private async Task PublishPendingTripChangeAsync(User user, TraewellingStatusKey key, TrawellingStatus status, bool pendingUpserted, bool pendingRemoved)
        {
            try
            {
                if (pendingUpserted)
                {
                    var trip = await _mapper.MapStatusToTripDtoAsync(status);
                    if (trip == null)
                        return;
                    trip.Id = key.ToString();
                    _memoryCache.Set(TripCacheKey(user.Id, key), trip, TimeSpan.FromMinutes(30));
                    await _traewellingHubContext.Clients.User(user.Id.ToString())
                        .SendAsync(TraewellingHub.PendingTripUpsertedMethod, JsonConvert.SerializeObject(trip, ApiJsonSettings));
                }
                else if (pendingRemoved)
                {
                    await _traewellingHubContext.Clients.User(user.Id.ToString())
                        .SendAsync(TraewellingHub.PendingTripRemovedMethod, key.ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing live Träwelling update for status {StatusId}, user {UserId}", key, user.Id);
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
        private async Task<bool> UpstreamDiffersAsync(User user, TraewellingStatusKey key, TrawellingStatus status)
        {
            var instance = await FindImportedInstanceAsync(user, key);
            if (instance == null)
            {
                return false; // nothing imported to conflict with
            }
            // Trips imported before Träwelling had UUIDs pick theirs up the next time their status
            // comes by, so the numeric ids can be phased out without a bulk lookup.
            if (instance.TrawellingStatusUuid == null && key.Uuid != null)
            {
                instance.TrawellingStatusUuid = key.Uuid;
                await _dbContext.SaveChangesAsync();
            }
            return TraewellingTripMapper.UpstreamDiffers(instance, await _mapper.MapStatusToTripDtoAsync(status));
        }

        private TrawellingInboxStatus UpsertInboxRow(TrawellingInboxStatus row, int userId, TraewellingStatusKey key, string payloadJson, TrawellingStatus status, TrawellingInboxState state)
        {
            if (row == null)
            {
                row = new TrawellingInboxStatus
                {
                    UserId = userId,
                    ReceivedAt = DateTime.UtcNow,
                };
                _dbContext.TrawellingInboxStatuses.Add(row);
            }
            row.TrawellingStatusId ??= key.Id;
            row.TrawellingStatusUuid ??= key.Uuid;
            row.PayloadJson = payloadJson;
            row.State = state;
            row.Source = TrawellingInboxSource.Webhook;
            row.DepartureAt = TraewellingTripMapper.GetStatusDeparture(status);
            row.LastEventAt = DateTime.UtcNow;
            return row;
        }

        public async Task<bool> IgnoreStatusAsync(User user, TraewellingStatusKey key)
        {
            try
            {
                // Both ids from the inbox row, which is removed below.
                var inboxRow = await FindInboxRowAsync(user, key);
                if (inboxRow != null)
                    key = TraewellingStatusKey.Of(inboxRow);

                if (await _dbContext.TrawellingIgnoredStatuses.Where(tis => tis.UserId == user.Id).WhereStatus(key).AnyAsync())
                {
                    _logger.LogInformation("Status {StatusId} is already ignored by user {UserId}", key, user.Id);
                    return false; // Already ignored
                }

                _dbContext.TrawellingIgnoredStatuses.Add(new TrawellingIgnoredStatus
                {
                    UserId = user.Id,
                    TrawellingStatusId = key.Id,
                    TrawellingStatusUuid = key.Uuid,
                    IgnoredAt = DateTime.UtcNow
                });
                await _dbContext.SaveChangesAsync();
                await RemoveFromInboxAsync(user.Id, key);

                _logger.LogInformation("Successfully ignored status {StatusId} for user {UserId}", key, user.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ignoring status {StatusId} for user {UserId}", key, user.Id);
                return false;
            }
        }

        public async Task<List<RouteInstance>> GetRouteInstancesByDateAsync(User user, DateTime date)
        {
            try
            {
                var query = _dbContext.RouteInstances
                    .Include(ri => ri.Route)
                    .Where(ri => !ri.TrawellingStatusId.HasValue && ri.TrawellingStatusUuid == null)
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

        public async Task<RouteInstance> LinkStatusToRouteInstanceAsync(User user, TraewellingStatusKey key, int routeInstanceId)
        {
            try
            {
                // Both ids from the inbox row, which is removed below.
                var inboxRow = await FindInboxRowAsync(user, key);
                if (inboxRow != null)
                    key = TraewellingStatusKey.Of(inboxRow);

                // Check if the Träwelling status is already linked to another of this user's
                // RouteInstances (two OVDB accounts may share one Träwelling account)
                var existingLink = await FindImportedInstanceAsync(user, key);
                if (existingLink != null)
                {
                    _logger.LogWarning("Träwelling status {StatusId} is already linked to RouteInstance {ExistingRouteInstanceId}",
                        key, existingLink.RouteInstanceId);
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
                var statusData = await GetStatusAsync(user, key);
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

                routeInstance.TrawellingStatusId = key.Id;
                routeInstance.TrawellingStatusUuid = key.Uuid;
                await _dbContext.SaveChangesAsync();
                await RemoveFromInboxAsync(user.Id, key);

                _logger.LogInformation("Successfully linked Träwelling status {StatusId} to RouteInstance {RouteInstanceId}",
                    key, routeInstanceId);

                return routeInstance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error linking Träwelling status {StatusId} to RouteInstance {RouteInstanceId}",
                    key, routeInstanceId);
                return null;
            }
        }

        private async Task<TrawellingTripDto> GetStatusAsync(User user, TraewellingStatusKey key)
        {
            try
            {

                if (_memoryCache.TryGetValue(TripCacheKey(user.Id, key), out TrawellingTripDto cachedStatus))
                {
                    return cachedStatus;
                }

                // Cache miss or expired - fetch from API
                var (_, status) = await _api.FetchStatusRawAsync(user, key);
                if (status == null)
                    return null;

                var mapped = await _mapper.MapStatusToTripDtoAsync(status);
                if (mapped != null)
                    mapped.Id = key.ToString();
                _memoryCache.Set(TripCacheKey(user.Id, key), mapped, TimeSpan.FromMinutes(30));
                return mapped;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Träwelling status {StatusId}", key);
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
                    .Where(ri => (ri.TrawellingStatusId.HasValue || ri.TrawellingStatusUuid != null) &&
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
                        var statusData = await GetStatusAsync(user, new TraewellingStatusKey(instance.TrawellingStatusId, instance.TrawellingStatusUuid));
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