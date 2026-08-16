using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using NetTopologySuite.Simplify;
using OVDB_database.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OV_DB.Services;

/// <summary>
/// Why a station and a trip are thought to belong together, weakest first. The order is deliberately
/// not the obvious one: a route endpoint beats a calling pattern, because starting or ending a
/// journey somewhere means you stood on the platform, while the train merely calling somewhere does
/// not.
/// </summary>
public enum VisitEvidence
{
    /// <summary>The line passes within the threshold. Cannot tell stopping from passing through.</summary>
    Proximity = 0,
    /// <summary>
    /// The train called here, according to the operator's own calling pattern. Supplied by the
    /// importer from Träwelling stopovers or OSM stop members — it cannot be derived from geometry,
    /// which is exactly why it is worth fetching.
    /// </summary>
    Stopover = 1,
    /// <summary>The journey started or ended here, so you were on the platform.</summary>
    RouteEndpoint = 2
}

/// <summary>A trip that might explain being at a station.</summary>
public sealed record TripCandidate(
    int RouteInstanceId,
    int RouteId,
    string RouteName,
    string From,
    string To,
    DateTime Date,
    VisitEvidence Evidence,
    double DistanceMetres);

/// <summary>A station a trip might explain having visited.</summary>
public sealed record StationCandidate(
    int StationId,
    string StationName,
    VisitEvidence Evidence,
    double DistanceMetres);

/// <summary>A station an operator says a train calls at, as named by the source it came from.</summary>
public readonly record struct StopPoint(string Name, double Lattitude, double Longitude);

public interface IStationTripMatcher
{
    Task<IReadOnlyList<TripCandidate>> FindTripsForStationAsync(int userId, int stationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StationCandidate>> FindStationsForTripAsync(int userId, int routeInstanceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StationCandidate>> MatchStopsAsync(IEnumerable<StopPoint> stops, CancellationToken cancellationToken = default);
}

/// <summary>
/// The one primitive behind backfill, import suggestions and dating, called in either direction:
/// which trips explain being at this station, and which stations does this trip explain?
/// </summary>
/// <remarks>
/// This service <b>only ever proposes</b>. It holds no reference to <see cref="IStationVisitService"/>
/// and writes nothing: marking a station stays an explicit user action, and inference may not
/// shortcut that. Both directions are computed on demand and never stored — a candidate is an
/// opinion about current data, and stale opinions are worse than none.
/// </remarks>
public class StationTripMatcher(OVDBDatabaseContext dbContext, IMatcherIndexCache indexCache) : IStationTripMatcher
{
    /// <summary>
    /// Measured: in a 400-station sample, 93.5% of stations have no other active station within
    /// 300 m, so a hit is usually unambiguous. Revisit if tram or metro stops are ever imported —
    /// that assumption is doing the work here, not the geometry.
    /// </summary>
    private const double ProximityMetres = 300.0;

    /// <summary>Coarse enough to halve the index, fine enough to be noise against 300 m.</summary>
    private const double SimplifyMetres = 50.0;

    /// <summary>
    /// How far an upstream stop may sit from the OVDB station it means. Tighter than the proximity
    /// threshold because this is identity matching, not "did we pass nearby".
    /// </summary>
    private const double StopMatchMetres = 250.0;

    /// <summary>
    /// The wider radius allowed when the names also agree. Large interchanges are a kilometre end
    /// to end and the two sources rarely pick the same point on them.
    /// </summary>
    private const double StopNameMatchMetres = 1000.0;

    private const double MetresPerDegreeLatitude = 111_320.0;

    public async Task<IReadOnlyList<TripCandidate>> FindTripsForStationAsync(int userId, int stationId, CancellationToken cancellationToken = default)
    {
        var station = await dbContext.Stations.AsNoTracking()
            .Where(s => s.Id == stationId)
            .Select(s => new { s.Id, s.Name, s.Lattitude, s.Longitude })
            .SingleOrDefaultAsync(cancellationToken);
        if (station == null)
        {
            return [];
        }

        var index = await GetIndexAsync(cancellationToken);

        // Nearest approach per route, not per segment: a line that runs alongside a station for a
        // while would otherwise be reported many times over.
        var nearest = new Dictionary<int, double>();
        foreach (var segment in index.Segments.Query(BoxAround(station.Lattitude, station.Longitude, ProximityMetres)))
        {
            var distance = DistanceToSegmentMetres(station.Lattitude, station.Longitude, segment);
            if (distance > ProximityMetres)
            {
                continue;
            }
            if (!nearest.TryGetValue(segment.RouteId, out var best) || distance < best)
            {
                nearest[segment.RouteId] = distance;
            }
        }

        if (nearest.Count == 0)
        {
            return [];
        }

        var routeIds = nearest.Keys.ToList();
        var trips = await dbContext.RouteInstances.AsNoTracking()
            .Where(ri => routeIds.Contains(ri.RouteId))
            // Scoping happens here rather than in the index: a route the user does not own simply
            // produces no trips.
            .Where(ri => ri.Route.RouteMaps.Any(rm => rm.Map.UserId == userId))
            .Select(ri => new
            {
                ri.RouteInstanceId,
                ri.RouteId,
                ri.Date,
                ri.Route.Name,
                ri.Route.From,
                ri.Route.To
            })
            .ToListAsync(cancellationToken);

        var candidates = trips.Select(t => new TripCandidate(
            t.RouteInstanceId,
            t.RouteId,
            t.Name,
            t.From,
            t.To,
            t.Date,
            EvidenceFor(index, t.RouteId, station.Name, t.From, t.To, station.Lattitude, station.Longitude),
            nearest[t.RouteId]));

        // Oldest first: the question this answers is "which trip first brought me here", and the
        // backfill preselects the earliest. Evidence rides along for the caller to group by.
        return candidates.OrderBy(c => c.Date).ThenByDescending(c => c.Evidence).ToList();
    }

    public async Task<IReadOnlyList<StationCandidate>> FindStationsForTripAsync(int userId, int routeInstanceId, CancellationToken cancellationToken = default)
    {
        var trip = await dbContext.RouteInstances.AsNoTracking()
            .Where(ri => ri.RouteInstanceId == routeInstanceId)
            .Where(ri => ri.Route.RouteMaps.Any(rm => rm.Map.UserId == userId))
            .Select(ri => new { ri.RouteId, ri.Route.From, ri.Route.To, ri.Route.LineString })
            .SingleOrDefaultAsync(cancellationToken);
        if (trip?.LineString == null)
        {
            return [];
        }

        var index = await GetIndexAsync(cancellationToken);
        var line = Simplify(trip.LineString);
        var coordinates = line.Coordinates;

        var nearest = new Dictionary<int, double>();
        for (var i = 0; i + 1 < coordinates.Length; i++)
        {
            var segment = new RouteSegment(trip.RouteId, coordinates[i].X, coordinates[i].Y, coordinates[i + 1].X, coordinates[i + 1].Y);
            foreach (var candidate in index.Stations.Query(BoxAroundSegment(segment, ProximityMetres)))
            {
                var distance = DistanceToSegmentMetres(candidate.Lattitude, candidate.Longitude, segment);
                if (distance > ProximityMetres)
                {
                    continue;
                }
                if (!nearest.TryGetValue(candidate.StationId, out var best) || distance < best)
                {
                    nearest[candidate.StationId] = distance;
                }
            }
        }

        if (nearest.Count == 0)
        {
            return [];
        }

        var stationIds = nearest.Keys.ToList();
        var stations = await dbContext.Stations.AsNoTracking()
            .Where(s => stationIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name, s.Lattitude, s.Longitude })
            .ToListAsync(cancellationToken);

        var endpoints = index.RouteEndpoints.TryGetValue(trip.RouteId, out var ends) ? ends : default;
        var results = stations.Select(s => new StationCandidate(
            s.Id,
            s.Name,
            EvidenceFrom(endpoints, s.Name, trip.From, trip.To, s.Lattitude, s.Longitude),
            nearest[s.Id]));

        // Strongest first here: the import list is read top-down and the weak tier is the long one.
        return results.OrderByDescending(r => r.Evidence).ThenBy(r => r.DistanceMetres).ToList();
    }

    /// <summary>
    /// Turns an operator's calling pattern into OVDB stations. Unlike the two geometric directions,
    /// this says the train <em>stopped</em>, which is the difference between a suggestion worth
    /// making and one that is merely nearby.
    /// </summary>
    /// <remarks>
    /// Matching is by position rather than name: Träwelling, OSM and OVDB disagree about names
    /// constantly ("Utrecht Centraal" / "Utrecht CS" / "Utrecht Centraal Station"), while the
    /// coordinates agree to within a platform's length. A name match widens the radius rather than
    /// replacing it, for the big stations where the two sources pick different reference points.
    /// </remarks>
    public async Task<IReadOnlyList<StationCandidate>> MatchStopsAsync(IEnumerable<StopPoint> stops, CancellationToken cancellationToken = default)
    {
        var index = await GetIndexAsync(cancellationToken);
        var matched = new Dictionary<int, double>();

        foreach (var stop in stops)
        {
            StationPoint? best = null;
            var bestDistance = double.MaxValue;

            foreach (var station in index.Stations.Query(BoxAround(stop.Lattitude, stop.Longitude, StopNameMatchMetres)))
            {
                var distance = DistanceMetres(stop.Lattitude, stop.Longitude, station.Lattitude, station.Longitude);
                if (distance > StopNameMatchMetres || distance >= bestDistance)
                {
                    continue;
                }
                best = station;
                bestDistance = distance;
            }

            if (best == null)
            {
                continue;
            }

            // Beyond the tight radius, only take it if the names agree.
            if (bestDistance > StopMatchMetres && !await NameAgreesAsync(best.Value.StationId, stop.Name, cancellationToken))
            {
                continue;
            }

            if (!matched.TryGetValue(best.Value.StationId, out var existing) || bestDistance < existing)
            {
                matched[best.Value.StationId] = bestDistance;
            }
        }

        if (matched.Count == 0)
        {
            return [];
        }

        var ids = matched.Keys.ToList();
        var names = await dbContext.Stations.AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToListAsync(cancellationToken);

        return names
            .Select(s => new StationCandidate(s.Id, s.Name, VisitEvidence.Stopover, matched[s.Id]))
            .OrderBy(c => c.StationName)
            .ToList();
    }

    private async Task<bool> NameAgreesAsync(int stationId, string stopName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stopName))
        {
            return false;
        }
        var name = await dbContext.Stations.AsNoTracking()
            .Where(s => s.Id == stationId)
            .Select(s => s.Name)
            .SingleOrDefaultAsync(cancellationToken);
        return NameMatches(name, stopName);
    }

    private Task<MatcherIndex> GetIndexAsync(CancellationToken cancellationToken) =>
        indexCache.GetAsync(BuildIndexAsync, cancellationToken);

    private async Task<MatcherIndex> BuildIndexAsync(CancellationToken cancellationToken)
    {
        var segments = new STRtree<RouteSegment>();
        var endpoints = new Dictionary<int, (double, double, double, double)>();

        // Streamed rather than loaded: whole geometry is 382 MB and simplified geometry is 65 MB, so
        // materialising the list first would spike peak memory by six times the steady state for no
        // reason. Each route is simplified and its original discarded as we go.
        var routes = dbContext.Routes.AsNoTracking()
            .Where(r => r.LineString != null)
            .Select(r => new { r.RouteId, r.LineString })
            .AsAsyncEnumerable();

        await foreach (var route in routes.WithCancellation(cancellationToken))
        {
            var coordinates = Simplify(route.LineString).Coordinates;
            if (coordinates.Length < 2)
            {
                continue;
            }

            for (var i = 0; i + 1 < coordinates.Length; i++)
            {
                var a = coordinates[i];
                var b = coordinates[i + 1];
                var segment = new RouteSegment(route.RouteId, a.X, a.Y, b.X, b.Y);
                segments.Insert(EnvelopeOf(segment), segment);
            }

            var first = coordinates[0];
            var last = coordinates[^1];
            endpoints[route.RouteId] = (first.X, first.Y, last.X, last.Y);
        }
        segments.Build();

        var stations = new STRtree<StationPoint>();
        var stationRows = dbContext.Stations.AsNoTracking()
            .Where(s => !s.Hidden && !s.Special)
            .Select(s => new { s.Id, s.Lattitude, s.Longitude })
            .AsAsyncEnumerable();

        await foreach (var station in stationRows.WithCancellation(cancellationToken))
        {
            stations.Insert(
                new Envelope(station.Longitude, station.Longitude, station.Lattitude, station.Lattitude),
                new StationPoint(station.Id, station.Longitude, station.Lattitude));
        }
        stations.Build();

        return new MatcherIndex
        {
            Segments = segments,
            Stations = stations,
            RouteEndpoints = endpoints
        };
    }

    private static Geometry Simplify(Geometry line) =>
        DouglasPeuckerSimplifier.Simplify(line, SimplifyMetres / MetresPerDegreeLatitude);

    private VisitEvidence EvidenceFor(MatcherIndex index, int routeId, string stationName, string from, string to, double lattitude, double longitude)
    {
        var endpoints = index.RouteEndpoints.TryGetValue(routeId, out var ends) ? ends : default;
        return EvidenceFrom(endpoints, stationName, from, to, lattitude, longitude);
    }

    /// <summary>
    /// Endpoint by geometry or by name. Both are offered because they disagree usefully: measured on
    /// this data, names match 21% of the time and geometric endpoints 27%, and the union catches
    /// renamed stations as well as routes that were clipped short of the platform.
    /// </summary>
    private static VisitEvidence EvidenceFrom((double X1, double Y1, double X2, double Y2) endpoints, string stationName, string from, string to, double lattitude, double longitude)
    {
        if (NameMatches(stationName, from) || NameMatches(stationName, to))
        {
            return VisitEvidence.RouteEndpoint;
        }

        if (endpoints != default)
        {
            var start = DistanceMetres(lattitude, longitude, endpoints.Y1, endpoints.X1);
            var end = DistanceMetres(lattitude, longitude, endpoints.Y2, endpoints.X2);
            if (Math.Min(start, end) <= ProximityMetres)
            {
                return VisitEvidence.RouteEndpoint;
            }
        }

        return VisitEvidence.Proximity;
    }

    private static bool NameMatches(string stationName, string endpointName) =>
        !string.IsNullOrWhiteSpace(stationName)
        && !string.IsNullOrWhiteSpace(endpointName)
        && string.Equals(stationName.Trim(), endpointName.Trim(), StringComparison.OrdinalIgnoreCase);

    private static Envelope EnvelopeOf(RouteSegment segment) =>
        new(Math.Min(segment.X1, segment.X2), Math.Max(segment.X1, segment.X2),
            Math.Min(segment.Y1, segment.Y2), Math.Max(segment.Y1, segment.Y2));

    private static Envelope BoxAroundSegment(RouteSegment segment, double metres)
    {
        var envelope = EnvelopeOf(segment);
        var dLat = metres / MetresPerDegreeLatitude;
        var dLon = dLat / Math.Max(Math.Cos(envelope.Centre.Y * Math.PI / 180.0), 0.01);
        envelope.ExpandBy(dLon, dLat);
        return envelope;
    }

    private static Envelope BoxAround(double lattitude, double longitude, double metres)
    {
        var dLat = metres / MetresPerDegreeLatitude;
        var dLon = dLat / Math.Max(Math.Cos(lattitude * Math.PI / 180.0), 0.01);
        return new Envelope(longitude - dLon, longitude + dLon, lattitude - dLat, lattitude + dLat);
    }

    /// <summary>
    /// Equirectangular around the point of interest. Good to well under a metre at these distances
    /// and far cheaper than a geodesic, which matters when it runs once per candidate segment.
    /// </summary>
    private static double DistanceToSegmentMetres(double lattitude, double longitude, RouteSegment segment)
    {
        var scale = Math.Cos(lattitude * Math.PI / 180.0) * MetresPerDegreeLatitude;
        var ax = (segment.X1 - longitude) * scale;
        var ay = (segment.Y1 - lattitude) * MetresPerDegreeLatitude;
        var bx = (segment.X2 - longitude) * scale;
        var by = (segment.Y2 - lattitude) * MetresPerDegreeLatitude;

        var dx = bx - ax;
        var dy = by - ay;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= double.Epsilon)
        {
            return Math.Sqrt(ax * ax + ay * ay);
        }

        // Projection of the origin onto the segment, clamped to its ends.
        var t = Math.Clamp(-(ax * dx + ay * dy) / lengthSquared, 0.0, 1.0);
        var cx = ax + t * dx;
        var cy = ay + t * dy;
        return Math.Sqrt(cx * cx + cy * cy);
    }

    private static double DistanceMetres(double lattitude, double longitude, double otherLattitude, double otherLongitude)
    {
        var scale = Math.Cos(lattitude * Math.PI / 180.0) * MetresPerDegreeLatitude;
        var dx = (otherLongitude - longitude) * scale;
        var dy = (otherLattitude - lattitude) * MetresPerDegreeLatitude;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
