using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using NetTopologySuite.Operation.Distance;

namespace NetworkLab;

public sealed record LabRoute(int RouteId, string Name, LineString Geometry, HashSet<NetworkMode> Modes);

/// <summary>
/// Matches routes against a mode's edges at several tolerances at once: per edge sample the
/// distances to the route once, then derive the covered fraction for every tolerance from the
/// same distances. Each edge keeps the best fraction any route achieved, per tolerance.
/// </summary>
public static class Matcher
{
    public static readonly double[] Tolerances = [10, 15, 20, 30];
    /// <summary>v1 display threshold; kept only so the report can still show the binary view
    /// next to the exact one. v2 has no coverage threshold.</summary>
    public const double CoverFractionThreshold = 0.75;
    public const double SampleSpacingM = 25;
    public const int MinStoredSamples = 4;
    private const double RouteWindowM = 5_000;

    private static readonly GeometryFactory Factory = new();

    public sealed class ModeIndex
    {
        public List<NetworkEdgeDraft> Edges;
        public Coordinate[][] Samples;          // projected sample points per edge
        /// <summary>
        /// Per edge, per sample: extra probe points on the edge's hidden twin tracks (projected),
        /// or null where there are none. A sample counts as covered when the route is within
        /// tolerance of the sample OR of any of its probes — riding the physical track that the
        /// parallel dedup hid must cover the corridor even when the tracks are further apart
        /// than the tolerance.
        /// </summary>
        public Coordinate[][][] TwinProbes;
        public STRtree<int> Tree;               // edge index by projected envelope

        /// <summary>
        /// Per edge, per tolerance: which samples any route so far has covered. Coverage must be
        /// the UNION across routes: at a through-station the arriving route covers one half of
        /// the station-approach edge and the departing route the other half — a per-route max
        /// would call the edge uncovered even though every metre was ridden (found at
        /// Stadskanaal with the STAR rides).
        /// </summary>
        public bool[][][] Covered;
        public int[][] CoveredCount;            // popcount cache of Covered
        public double[] LengthM;                // projected polyline length per edge, for fractions

        public void FinalizeCoverage()
        {
            for (var e = 0; e < Edges.Count; e++)
            {
                var edge = Edges[e];
                edge.CoveredCounts = new int[Tolerances.Length];
                edge.Intervals = new List<double[]>[Tolerances.Length];
                for (var t = 0; t < Tolerances.Length; t++)
                {
                    var count = CoveredCount[e][t];
                    edge.CoveredCounts[t] = count;
                    // Storage floor (design 5.1): a perpendicular crossing touches 1-2 samples
                    // and must not count; below MinStoredSamples the row would not exist at all.
                    edge.BestFraction[t] = count >= MinStoredSamples ? (double)count / Samples[e].Length : 0;
                    edge.Intervals[t] = count >= MinStoredSamples ? RunsToIntervals(Covered[e][t], LengthM[e]) : new List<double[]>();
                }
            }
        }

        /// <summary>
        /// Run-length encode covered samples into [start, end] fractions of edge length. A run
        /// of samples a..b covers the line between them plus half a spacing on each side, so a
        /// fully covered edge yields exactly [0, 1].
        /// </summary>
        private static List<double[]> RunsToIntervals(bool[] bits, double totalM)
        {
            var result = new List<double[]>();
            var i = 0;
            while (i < bits.Length)
            {
                if (!bits[i]) { i++; continue; }
                var j = i;
                while (j + 1 < bits.Length && bits[j + 1]) j++;
                var startM = Math.Max(0, Math.Min(i * SampleSpacingM, totalM) - SampleSpacingM / 2);
                var endM = Math.Min(totalM, Math.Min(j * SampleSpacingM, totalM) + SampleSpacingM / 2);
                result.Add([Math.Round(startM / totalM, 3), Math.Round(endM / totalM, 3)]);
                i = j + 1;
            }
            return result;
        }
    }

    public static ModeIndex BuildIndex(List<NetworkEdgeDraft> edges, Geo.LocalProjection projection)
    {
        var index = new ModeIndex
        {
            Edges = edges,
            Samples = new Coordinate[edges.Count][],
            TwinProbes = new Coordinate[edges.Count][][],
            Tree = new STRtree<int>(),
            Covered = new bool[edges.Count][][],
            CoveredCount = new int[edges.Count][],
            LengthM = new double[edges.Count],
        };
        for (var i = 0; i < edges.Count; i++)
        {
            var projected = projection.Project(edges[i].Geometry.Coordinates);
            index.Samples[i] = Sample(projected);
            for (var k = 1; k < projected.Length; k++)
                index.LengthM[i] += projected[k - 1].Distance(projected[k]);
            index.TwinProbes[i] = BuildTwinProbes(index.Samples[i], edges[i], projection);
            index.Covered[i] = new bool[Tolerances.Length][];
            for (var t = 0; t < Tolerances.Length; t++)
                index.Covered[i][t] = new bool[index.Samples[i].Length];
            index.CoveredCount[i] = new int[Tolerances.Length];
            var envelope = new Envelope(projected[0]);
            foreach (var c in projected)
                envelope.ExpandToInclude(c);
            if (index.TwinProbes[i] != null)
                foreach (var probes in index.TwinProbes[i])
                    if (probes != null)
                        foreach (var p in probes)
                            envelope.ExpandToInclude(p);
            index.Tree.Insert(envelope, i);
        }
        index.Tree.Build();
        return index;
    }

    private const double TwinProbeMaxDistM = 80; // twins live within 2×hideDist of the survivor

    /// <summary>For each sample, the nearest point on each hidden twin polyline, if close by.</summary>
    private static Coordinate[][] BuildTwinProbes(Coordinate[] samples, NetworkEdgeDraft edge, Geo.LocalProjection projection)
    {
        if (edge.HiddenTwins == null || edge.HiddenTwins.Count == 0)
            return null;
        var twins = edge.HiddenTwins.Select(t => projection.Project(t)).ToList();
        var result = new Coordinate[samples.Length][];
        for (var s = 0; s < samples.Length; s++)
        {
            List<Coordinate> probes = null;
            foreach (var twin in twins)
            {
                var (dist, nearest) = NearestOnPolyline(samples[s], twin);
                if (dist <= TwinProbeMaxDistM)
                    (probes ??= new List<Coordinate>()).Add(nearest);
            }
            result[s] = probes?.ToArray();
        }
        return result;
    }

    private static (double Dist, Coordinate Nearest) NearestOnPolyline(Coordinate p, Coordinate[] coords)
    {
        var best = double.MaxValue;
        Coordinate bestPoint = coords[0];
        for (var k = 1; k < coords.Length; k++)
        {
            var a = coords[k - 1];
            var b = coords[k];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var len2 = dx * dx + dy * dy;
            var t = len2 <= 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2, 0, 1);
            var cx = a.X + t * dx;
            var cy = a.Y + t * dy;
            var d = Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy));
            if (d < best)
            {
                best = d;
                bestPoint = new Coordinate(cx, cy);
            }
        }
        return (best, bestPoint);
    }

    /// <summary>
    /// The match lattice (design 4.8): a UNIFORM chainage lattice over the projected polyline —
    /// sample i at chainage min(i·25 m, L), count = floor(L/25)+1, plus a final sample at L when
    /// L is not a lattice multiple. Assembly's SampleCount, the stored bitmaps and the read
    /// path's interval fractions (chainage_i / L) all derive from this one definition. The
    /// per-segment sampler used inside dedup/splitting is deliberately NOT reused here.
    /// </summary>
    public static Coordinate[] Sample(Coordinate[] coords)
    {
        var cumulative = new double[coords.Length];
        for (var i = 1; i < coords.Length; i++)
            cumulative[i] = cumulative[i - 1] + coords[i - 1].Distance(coords[i]);
        var total = cumulative[^1];

        var count = (int)Math.Floor(total / SampleSpacingM) + 1;
        var needsEnd = total - (count - 1) * SampleSpacingM > 1e-6;
        var samples = new Coordinate[count + (needsEnd ? 1 : 0)];
        var seg = 1;
        for (var i = 0; i < samples.Length; i++)
        {
            var chainage = Math.Min(i * SampleSpacingM, total);
            while (seg < coords.Length - 1 && cumulative[seg] < chainage)
                seg++;
            var segLen = cumulative[seg] - cumulative[seg - 1];
            var t = segLen <= 0 ? 0 : (chainage - cumulative[seg - 1]) / segLen;
            samples[i] = new Coordinate(
                coords[seg - 1].X + (coords[seg].X - coords[seg - 1].X) * t,
                coords[seg - 1].Y + (coords[seg].Y - coords[seg - 1].Y) * t);
        }
        return samples;
    }

    /// <summary>Chainage fraction of sample i on an edge of projected length totalM.</summary>
    public static double SampleFraction(int i, double totalM) =>
        totalM <= 0 ? 0 : Math.Min(i * SampleSpacingM, totalM) / totalM;

    public static void MatchRoute(LabRoute route, ModeIndex index, Geo.LocalProjection projection)
    {
        var projectedRoute = Factory.CreateLineString(projection.Project(route.Geometry.Coordinates));
        var facetDistance = new IndexedFacetDistance(projectedRoute);
        var maxTolerance = Tolerances[^1];

        // Candidate edges via sliding windows over the route, because a cross-country route's
        // whole envelope would return half the network (same lesson as MatcherIndex).
        var candidates = new HashSet<int>();
        var routeCoords = projectedRoute.Coordinates;
        var window = new Envelope(routeCoords[0]);
        var windowKm = 0d;
        for (var i = 1; i < routeCoords.Length; i++)
        {
            window.ExpandToInclude(routeCoords[i]);
            windowKm += routeCoords[i - 1].Distance(routeCoords[i]);
            if (windowKm >= RouteWindowM || i == routeCoords.Length - 1)
            {
                window.ExpandBy(maxTolerance + 10);
                foreach (var edgeIdx in index.Tree.Query(window))
                    candidates.Add(edgeIdx);
                window = new Envelope(routeCoords[i]);
                windowKm = 0;
            }
        }

        foreach (var edgeIdx in candidates)
        {
            var samples = index.Samples[edgeIdx];
            if (index.CoveredCount[edgeIdx][0] == samples.Length)
                continue; // already fully covered at the tightest tolerance, nothing to improve

            var covered = index.Covered[edgeIdx];
            var probes = index.TwinProbes[edgeIdx];
            for (var s = 0; s < samples.Length; s++)
            {
                if (covered[0][s])
                    continue; // fully known-covered sample, skip the distance query
                var distance = facetDistance.Distance(Factory.CreatePoint(samples[s]));
                if (probes?[s] != null)
                {
                    foreach (var probe in probes[s])
                    {
                        if (distance <= Tolerances[0])
                            break;
                        distance = Math.Min(distance, facetDistance.Distance(Factory.CreatePoint(probe)));
                    }
                }
                for (var t = 0; t < Tolerances.Length; t++)
                {
                    if (distance <= Tolerances[t] && !covered[t][s])
                    {
                        covered[t][s] = true;
                        index.CoveredCount[edgeIdx][t]++;
                    }
                }
            }
        }
    }

    public static HashSet<NetworkMode> EligibleModes(string routeTrainlogType, string typeTrainlogType, bool? isTrain, string typeName)
    {
        var effective = FirstNonEmpty(routeTrainlogType, typeTrainlogType)
            ?? FromTypeName(typeName)
            ?? (isTrain == true ? "train" : null);
        return effective switch
        {
            // Trains also cover urban rail: tram-trains and S-Bahn-like lines blur the boundary,
            // and geometry keeps a mainline train from covering a street tramway anyway.
            // Every rail-bound mode covers BOTH networks; geometry decides what matches. The
            // asymmetric version broke on shared tracks: Köln Stadtbahn line 7 to Benzelrath
            // runs on railway=rail (ex-KFBE), so tram-typed rides must be able to cover Rail
            // edges, just as trains cover tram-train and S-Bahn-like urban track.
            "train" or "tram" or "metro" => [NetworkMode.Rail, NetworkMode.UrbanRail],
            _ => [],
        };
    }

    /// <summary>
    /// Name-based fallback for the many route types that have no TrainlogType set (dev data:
    /// "Metro", "Tram", "Trein", "Zug", ... all NULL). In the real app the fix is setting
    /// TrainlogType on the route type; this fallback quantifies how much that matters.
    /// </summary>
    private static string FromTypeName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;
        var name = typeName.ToLowerInvariant();
        if (name.Contains("metro") || name.Contains("subway") || name.Contains("u-bahn"))
            return "metro";
        if (name.Contains("tram"))
            return "tram";
        if (name.Contains("trein") || name.Contains("train") || name.Contains("zug") || name.Contains("rail"))
            return "train";
        return null;
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
