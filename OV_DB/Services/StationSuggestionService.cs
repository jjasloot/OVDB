using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OV_DB.Models;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OV_DB.Services;

/// <summary>
/// What an import's calling pattern turned into, and whether it could be read at all.
/// </summary>
/// <remarks>
/// The two are genuinely different and the user deserves to be told which: no stations means
/// there was nothing left to offer, while <c>Unavailable</c> means the question was
/// never answered, and those suggestions are not coming back — they are computed at import and
/// never stored.
/// </remarks>
public sealed record StationSuggestionResult(List<StationSuggestionDTO> Stations, bool Unavailable)
{
    /// <summary>Asked and answered; there was simply nothing to offer.</summary>
    public static StationSuggestionResult Nothing => new([], false);

    /// <summary>Never asked, or asked and not answered in time.</summary>
    public static StationSuggestionResult NotAvailable => new([], true);
}

public interface IStationSuggestionService
{
    Task<StationSuggestionResult> FromTrawellingStatusAsync(User user, string statusPayloadJson, CancellationToken cancellationToken = default);
    Task<List<StationSuggestionDTO>> FromStopsAsync(int userId, IEnumerable<StopPoint> stops, CancellationToken cancellationToken = default);
}

/// <summary>
/// Turns an operator's calling pattern into a list of stations the user has not marked.
/// </summary>
/// <remarks>
/// It suggests and nothing more — it cannot write a visit, and holds no reference to anything that
/// can. Suggestions are computed at import and not stored: a calling pattern is a fact about a trip,
/// and once the user has answered, keeping the question around only invites it to go stale.
/// </remarks>
public class StationSuggestionService(
    OVDBDatabaseContext dbContext,
    IStationTripMatcher matcher,
    ITrawellingService trawellingService,
    ITraewellingRateLimiter rateLimiter) : IStationSuggestionService
{
    /// <summary>
    /// How long the one upstream call is given. The trip is already saved by the time this runs,
    /// so the only thing waiting longer buys is suggestions — and the user is sitting in front of
    /// a screen that will not finish until this does. A second or two is a normal answer; anything
    /// beyond this is Träwelling being slow, and the rate limiter alone can sleep a minute.
    /// </summary>
    private static readonly TimeSpan StopoverFetchTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Reads the trip id out of a stored check-in payload and asks Träwelling what that trip calls
    /// at. The payload is already on hand at import, so this costs one API call rather than two.
    /// </summary>
    public async Task<StationSuggestionResult> FromTrawellingStatusAsync(User user, string statusPayloadJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(statusPayloadJson))
        {
            return StationSuggestionResult.Nothing;
        }

        TrawellingStatus status;
        try
        {
            status = JsonConvert.DeserializeObject<TrawellingStatus>(statusPayloadJson);
        }
        catch (JsonException)
        {
            return StationSuggestionResult.Nothing;
        }

        var tripId = status?.Checkin?.Trip ?? 0;
        if (tripId == 0)
        {
            return StationSuggestionResult.Nothing;
        }

        // Known to be blocked, so there is nothing to find out by trying: the call would sleep out
        // the window before it even left. Say so now rather than a minute from now.
        if (rateLimiter.IsLimited)
        {
            return StationSuggestionResult.NotAvailable;
        }

        List<TrawellingStopover> stopovers;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(StopoverFetchTimeout);
        try
        {
            stopovers = await trawellingService.GetTripStopoversAsync(user, tripId, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Ours, not the caller's: the request is still live and wants an answer, just without
            // the suggestions.
            return StationSuggestionResult.NotAvailable;
        }

        if (stopovers.Count == 0)
        {
            // Either the trip publishes no calling pattern or the fetch failed — GetTripStopoversAsync
            // reports both by answering with nothing. Told as one thing, because to the user they are:
            // this trip's calling pattern could not be read, so no stations are being offered.
            return StationSuggestionResult.NotAvailable;
        }

        // Only the section actually ridden. The endpoint answers with the whole trip, and the
        // stations before boarding or beyond where the user got off were never reached — offering
        // those invites marking a visit that did not happen.
        var ridden = RiddenSection(stopovers, status.Checkin?.Origin?.Station, status.Checkin?.Destination?.Station);

        var stops = ridden
            .Where(s => s.Station?.Latitude != null && s.Station?.Longitude != null)
            .Select(s => new StopPoint(s.Station.Name, s.Station.Latitude.Value, s.Station.Longitude.Value));

        return new StationSuggestionResult(await FromStopsAsync(user.Id, stops, cancellationToken), false);
    }

    /// <summary>
    /// The stopovers from where the user boarded to where they got off, inclusive.
    /// </summary>
    /// <remarks>
    /// Matched on station id rather than position, because a check-in names its own origin and
    /// destination and those can sit anywhere in the trip. If either cannot be found — an unusual
    /// payload, a trip that changed under us — the whole pattern is kept: over-offering is recoverable
    /// (the user says no), silently dropping the stations they did visit is not.
    /// </remarks>
    private static List<TrawellingStopover> RiddenSection(
        List<TrawellingStopover> stopovers, TrawellingStation origin, TrawellingStation destination)
    {
        if (origin == null || destination == null)
        {
            return stopovers;
        }

        var from = stopovers.FindIndex(s => s.Station?.Id == origin.Id);
        // Searched from the boarding point onward, so a trip that calls at one station twice takes
        // the arrival after boarding rather than one before it.
        var to = from < 0 ? -1 : stopovers.FindIndex(from, s => s.Station?.Id == destination.Id);

        return from < 0 || to < from ? stopovers : stopovers.GetRange(from, to - from + 1);
    }

    /// <summary>
    /// The stations a list of stops suggests, where the first and last stop are taken to be where the
    /// user boarded and got off.
    /// </summary>
    public async Task<List<StationSuggestionDTO>> FromStopsAsync(int userId, IEnumerable<StopPoint> stops, CancellationToken cancellationToken = default)
    {
        var stopList = stops as IList<StopPoint> ?? stops.ToList();
        var candidates = await matcher.MatchStopsAsync(stopList, cancellationToken);
        if (candidates.Count == 0)
        {
            return [];
        }

        // Which stations the ends of the journey landed on, matched separately because unmatched
        // stops drop out of the list above: the first suggestion is not necessarily the first stop.
        // Getting this wrong is not cosmetic — the dialog defaults an endpoint to "boarded here", so
        // a station in the middle inheriting that offers the user a claim they never made.
        var endpointIds = new HashSet<int>();
        if (stopList.Count > 1)
        {
            foreach (var end in await matcher.MatchStopsAsync([stopList[0], stopList[^1]], cancellationToken))
            {
                endpointIds.Add(end.StationId);
            }
        }

        var ids = candidates.Select(c => c.StationId).ToList();

        // Already marked, or explicitly dismissed, is not a suggestion.
        var visited = await dbContext.StationVisits.AsNoTracking()
            .Where(sv => sv.UserId == userId && ids.Contains(sv.StationId))
            .Select(sv => sv.StationId)
            .ToListAsync(cancellationToken);
        var dismissed = await dbContext.StationSuggestionDismissals.AsNoTracking()
            .Where(d => d.UserId == userId && ids.Contains(d.StationId))
            .Select(d => d.StationId)
            .ToListAsync(cancellationToken);
        var excluded = visited.Concat(dismissed).ToHashSet();

        var positions = await dbContext.Stations.AsNoTracking()
            .Where(s => ids.Contains(s.Id) && !s.Hidden && !s.Special)
            .Select(s => new { s.Id, s.Lattitude, s.Longitude })
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        return candidates
            .Where(c => !excluded.Contains(c.StationId) && positions.ContainsKey(c.StationId))
            .Select(c => new StationSuggestionDTO
            {
                StationId = c.StationId,
                StationName = c.StationName,
                Lattitude = positions[c.StationId].Lattitude,
                Longitude = positions[c.StationId].Longitude,
                IsEndpoint = endpointIds.Contains(c.StationId),
                DistanceMetres = c.DistanceMetres
            })
            .ToList();
    }
}
