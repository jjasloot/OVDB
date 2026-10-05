using System;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using NetTopologySuite.Operation.Distance;

namespace OV_DB.Services.Network;

/// <summary>What the matcher needs of an edge; a projection of <see cref="OVDB_database.Models.NetworkEdge"/>.</summary>
public sealed record MatchableEdge(long Id, LineString Geometry, int SampleCount, MultiLineString HiddenTwins);

/// <summary>
/// One network's edges prepared for matching: lattice samples (exactly <c>SampleCount</c> per
/// edge, the stored contract), hidden-twin probes per sample, and a spatial index. Built once
/// per network version and reused across routes. Pure geometry, no database — see the tests.
/// </summary>
public sealed class NetworkMatchIndex
{
    public const double ToleranceM = 15;
    /// <summary>A row exists from this many covered samples (~100 m); a perpendicular crossing touches 1–2.</summary>
    public const int MinStoredSamples = 4;
    private const double TwinProbeMaxDistM = 80;   // twins live within 2×hideDist of the survivor
    private const double RouteWindowM = 5_000;

    private static readonly GeometryFactory Factory = new();

    private readonly long[] _edgeIds;
    private readonly Coordinate[][] _samples;
    private readonly Coordinate[][][] _probes;      // per edge, per sample: extra probe points or null
    private readonly STRtree<int> _tree = new();

    public NetworkGeo.LocalProjection Projection { get; }
    public int EdgeCount => _edgeIds.Length;

    public NetworkMatchIndex(IReadOnlyList<MatchableEdge> edges, NetworkGeo.LocalProjection projection)
    {
        Projection = projection;
        _edgeIds = new long[edges.Count];
        _samples = new Coordinate[edges.Count][];
        _probes = new Coordinate[edges.Count][][];
        for (var i = 0; i < edges.Count; i++)
        {
            var edge = edges[i];
            _edgeIds[i] = edge.Id;
            var projected = projection.Project(edge.Geometry.Coordinates);
            _samples[i] = NetworkLattice.Sample(projected, edge.SampleCount);
            _probes[i] = BuildProbes(_samples[i], edge.HiddenTwins, projection);

            var envelope = ParallelDedup.EnvelopeOf(projected);
            if (_probes[i] != null)
                foreach (var probes in _probes[i])
                    if (probes != null)
                        foreach (var p in probes)
                            envelope.ExpandToInclude(p);
            _tree.Insert(envelope, i);
        }
        _tree.Build();
    }

    /// <summary>
    /// Match one route: per candidate edge, the samples within tolerance of the route (directly
    /// or via a hidden-twin probe). Only edges with at least <see cref="MinStoredSamples"/>
    /// covered samples (or, on a shorter edge, all of them) are returned.
    /// </summary>
    public Dictionary<long, bool[]> Match(LineString routeGeometry)
    {
        var result = new Dictionary<long, bool[]>();
        if (routeGeometry == null || routeGeometry.Coordinates.Length < 2)
            return result;

        var routeCoords = Projection.Project(routeGeometry.Coordinates);
        var route = Factory.CreateLineString(routeCoords);
        var facetDistance = new IndexedFacetDistance(route);

        // Candidates via sliding windows over the route, never its whole envelope: a
        // cross-country route's bounding box covers half the network.
        var candidates = new HashSet<int>();
        var window = new Envelope(routeCoords[0]);
        var windowM = 0d;
        for (var i = 1; i < routeCoords.Length; i++)
        {
            window.ExpandToInclude(routeCoords[i]);
            windowM += routeCoords[i - 1].Distance(routeCoords[i]);
            if (windowM >= RouteWindowM || i == routeCoords.Length - 1)
            {
                window.ExpandBy(ToleranceM + 10);
                foreach (var edgeIdx in _tree.Query(window))
                    candidates.Add(edgeIdx);
                window = new Envelope(routeCoords[i]);
                windowM = 0;
            }
        }

        foreach (var edgeIdx in candidates)
        {
            var samples = _samples[edgeIdx];
            var probes = _probes[edgeIdx];
            var bits = new bool[samples.Length];
            var count = 0;
            for (var s = 0; s < samples.Length; s++)
            {
                var distance = facetDistance.Distance(Factory.CreatePoint(samples[s]));
                if (distance > ToleranceM && probes?[s] != null)
                {
                    foreach (var probe in probes[s])
                    {
                        distance = Math.Min(distance, facetDistance.Distance(Factory.CreatePoint(probe)));
                        if (distance <= ToleranceM)
                            break;
                    }
                }
                if (distance <= ToleranceM)
                {
                    bits[s] = true;
                    count++;
                }
            }
            if (IsStored(count, samples.Length))
                result[_edgeIds[edgeIdx]] = bits;
        }
        return result;
    }

    /// <summary>
    /// Enough evidence to store a row: <see cref="MinStoredSamples"/> covered samples, or, on an
    /// edge too short to have that many (a sub-50 m junction connector), every sample. Without
    /// the second clause such slivers could never be covered and completion could never reach 100%.
    /// </summary>
    internal static bool IsStored(int covered, int sampleCount) =>
        covered >= MinStoredSamples || (sampleCount < MinStoredSamples && covered == sampleCount && covered > 0);

    private static Coordinate[][] BuildProbes(Coordinate[] samples, MultiLineString hiddenTwins, NetworkGeo.LocalProjection projection)
    {
        if (hiddenTwins == null || hiddenTwins.IsEmpty)
            return null;
        var twins = new List<Coordinate[]>();
        for (var g = 0; g < hiddenTwins.NumGeometries; g++)
            twins.Add(projection.Project(hiddenTwins.GetGeometryN(g).Coordinates));

        var result = new Coordinate[samples.Length][];
        var any = false;
        for (var s = 0; s < samples.Length; s++)
        {
            List<Coordinate> probes = null;
            foreach (var twin in twins)
            {
                var (dist, _, nearest) = ParallelDedup.DistanceToPolyline(samples[s], twin);
                if (dist <= TwinProbeMaxDistM)
                    (probes ??= new List<Coordinate>()).Add(nearest);
            }
            if (probes != null)
            {
                result[s] = probes.ToArray();
                any = true;
            }
        }
        return any ? result : null;
    }
}
