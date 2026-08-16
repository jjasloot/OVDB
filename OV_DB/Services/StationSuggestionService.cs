using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OV_DB.Models;
using OVDB_database.Database;
using OVDB_database.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OV_DB.Services;

public interface IStationSuggestionService
{
    Task<List<StationSuggestionDTO>> FromTrawellingStatusAsync(User user, string statusPayloadJson, CancellationToken cancellationToken = default);
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
    ITrawellingService trawellingService) : IStationSuggestionService
{
    /// <summary>
    /// Reads the trip id out of a stored check-in payload and asks Träwelling what that trip calls
    /// at. The payload is already on hand at import, so this costs one API call rather than two.
    /// </summary>
    public async Task<List<StationSuggestionDTO>> FromTrawellingStatusAsync(User user, string statusPayloadJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(statusPayloadJson))
        {
            return [];
        }

        TrawellingStatus status;
        try
        {
            status = JsonConvert.DeserializeObject<TrawellingStatus>(statusPayloadJson);
        }
        catch (JsonException)
        {
            return [];
        }

        var tripId = status?.Checkin?.Trip ?? 0;
        if (tripId == 0)
        {
            return [];
        }

        var stopovers = await trawellingService.GetTripStopoversAsync(user, tripId, cancellationToken);
        var stops = stopovers
            .Where(s => s.Station?.Latitude != null && s.Station?.Longitude != null)
            .Select(s => new StopPoint(s.Station.Name, s.Station.Latitude.Value, s.Station.Longitude.Value));

        return await FromStopsAsync(user.Id, stops, cancellationToken);
    }

    public async Task<List<StationSuggestionDTO>> FromStopsAsync(int userId, IEnumerable<StopPoint> stops, CancellationToken cancellationToken = default)
    {
        var candidates = await matcher.MatchStopsAsync(stops, cancellationToken);
        if (candidates.Count == 0)
        {
            return [];
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
                IsEndpoint = false,
                DistanceMetres = c.DistanceMetres
            })
            .ToList();
    }
}
