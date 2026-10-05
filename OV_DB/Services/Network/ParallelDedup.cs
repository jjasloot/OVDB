using System;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;

namespace OV_DB.Services.Network;

/// <summary>
/// Collapses parallel tracks of one line into a single drawn and counted line, per sample, with
/// no centerline synthesis. Validated in tools/NetworkLab (design doc 4.5).
///
/// A sample of an edge is a SHADOW when a higher-priority (longer; total order) edge of the same
/// passenger flag and compatible name runs bearing-aligned within hideDist of it. Shadow runs
/// leave the map and both sides of the km arithmetic but are kept as HIDDEN TWINS on the nearest
/// surviving edge, which the matcher probes so riding either physical track covers the corridor.
/// Visible samples are pulled to the mean of themselves and their aligned partners within
/// AverageDistM, so a track pair renders as one line down the corridor middle.
/// </summary>
public static class ParallelDedup
{
    public const double RailHideDistM = 25;
    public const double UrbanHideDistM = 35;
    private const double AverageDistM = 15;
    private const double BearingTolDeg = 20;
    private const double SampleSpacingM = 25;
    /// <summary>Runs shorter than this flip to their surroundings: crossover gaps must not shred edges.</summary>
    private const int SmoothRunSamples = 6; // ~150 m
    /// <summary>
    /// A visible run with shadow on BOTH sides is corridor flicker — the partner track weaving
    /// across the hide distance as a wide shared right-of-way narrows and widens. A genuine
    /// divergence ends at a junction or edge end instead of re-entering shadow, so it is untouched.
    /// </summary>
    private const int VisibleFlickerSamples = 40; // ~1 km
    private const double MinPieceKm = 0.05;

    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    public sealed record ShadowPiece(Coordinate[] Geo, bool IsPassenger);

    private sealed class Sampled
    {
        public Coordinate[] Geo;
        public Coordinate[] Proj;
        public double[] BearingDeg;
    }

    public static double HideDistFor(OVDB_database.Models.NetworkMode mode) =>
        mode == OVDB_database.Models.NetworkMode.UrbanRail ? UrbanHideDistM : RailHideDistM;

    public static List<NetworkEdgeDraft> Apply(List<NetworkEdgeDraft> edges, NetworkGeo.LocalProjection projection, double hideDistM, out List<ShadowPiece> shadowPieces)
    {
        shadowPieces = new List<ShadowPiece>();
        if (edges.Count < 2)
            return edges;

        var sampled = new Sampled[edges.Count];
        var tree = new STRtree<int>();
        for (var i = 0; i < edges.Count; i++)
        {
            sampled[i] = SampleAlongSegments(edges[i].Geometry.Coordinates, projection);
            tree.Insert(EnvelopeOf(sampled[i].Proj), i);
        }
        tree.Build();

        var rank = new int[edges.Count];
        var byLength = Enumerable.Range(0, edges.Count).OrderBy(i => edges[i].LengthKm).ThenBy(i => i).ToArray();
        for (var r = 0; r < byLength.Length; r++)
            rank[byLength[r]] = r;

        var result = new List<NetworkEdgeDraft>(edges.Count);
        for (var i = 0; i < edges.Count; i++)
        {
            var mine = sampled[i];
            var queryEnvelope = EnvelopeOf(mine.Proj);
            queryEnvelope.ExpandBy(hideDistM);
            var candidates = tree.Query(queryEnvelope)
                .Where(j => j != i
                    && edges[j].IsPassenger == edges[i].IsPassenger
                    && NamesCompatible(edges[i].Name, edges[j].Name))
                .OrderByDescending(j => rank[j])
                .ToList();
            if (candidates.Count == 0)
            {
                result.Add(edges[i]);
                continue;
            }

            var shadow = new bool[mine.Geo.Length];
            var adjustedGeo = (Coordinate[])mine.Geo.Clone();
            for (var s = 0; s < mine.Geo.Length; s++)
            {
                var sumX = mine.Proj[s].X;
                var sumY = mine.Proj[s].Y;
                var count = 1;
                foreach (var j in candidates)
                {
                    var partner = NearestAligned(mine.Proj[s], mine.BearingDeg[s], sampled[j], hideDistM);
                    if (partner == null)
                        continue;
                    if (rank[j] > rank[i])
                    {
                        shadow[s] = true;
                        break;
                    }
                    if (partner.Value.Dist <= AverageDistM)
                    {
                        sumX += partner.Value.Point.X;
                        sumY += partner.Value.Point.Y;
                        count++;
                    }
                }
                if (!shadow[s] && count > 1)
                    adjustedGeo[s] = projection.Unproject(new Coordinate(sumX / count, sumY / count));
            }

            SmoothRuns(shadow);
            FlipInteriorVisibleRuns(shadow);
            if (!shadow.Any(x => x))
            {
                result.Add(MakeEdge(edges[i], adjustedGeo, 0, adjustedGeo.Length) ?? edges[i]);
                continue;
            }

            var start = -1;
            for (var s = 0; s <= shadow.Length; s++)
            {
                var visible = s < shadow.Length && !shadow[s];
                if (visible && start < 0)
                    start = s;
                if (!visible && start >= 0)
                {
                    var piece = MakeEdge(edges[i], adjustedGeo, start, s);
                    if (piece != null)
                        result.Add(piece);
                    start = -1;
                }
            }
            var shadowStart = -1;
            for (var s = 0; s <= shadow.Length; s++)
            {
                var isShadow = s < shadow.Length && shadow[s];
                if (isShadow && shadowStart < 0)
                    shadowStart = s;
                if (!isShadow && shadowStart >= 0)
                {
                    // One sample of overlap on each side keeps probe coverage continuous.
                    var from = Math.Max(shadowStart - 1, 0);
                    var to = Math.Min(s + 1, shadow.Length);
                    if (to - from >= 2)
                        shadowPieces.Add(new ShadowPiece(mine.Geo[from..to], edges[i].IsPassenger));
                    shadowStart = -1;
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Attach each shadow piece to the nearest surviving edge of the same flag, as matcher probes.
    /// Must run on the FINAL edge list — after station splitting and the length cap have made
    /// their cuts — or twins would bind to edges that no longer exist.
    /// </summary>
    public static void AttachTwins(List<NetworkEdgeDraft> edges, List<ShadowPiece> shadowPieces, NetworkGeo.LocalProjection projection, double hideDistM)
    {
        if (shadowPieces.Count == 0 || edges.Count == 0)
            return;
        var tree = new STRtree<int>();
        var projected = new Coordinate[edges.Count][];
        for (var i = 0; i < edges.Count; i++)
        {
            projected[i] = projection.Project(edges[i].Geometry.Coordinates);
            tree.Insert(EnvelopeOf(projected[i]), i);
        }
        tree.Build();

        foreach (var piece in shadowPieces)
        {
            var mid = projection.Project(piece.Geo[piece.Geo.Length / 2]);
            var envelope = new Envelope(mid);
            envelope.ExpandBy(hideDistM * 2);
            var best = -1;
            var bestDist = double.MaxValue;
            foreach (var i in tree.Query(envelope))
            {
                if (edges[i].IsPassenger != piece.IsPassenger)
                    continue;
                var d = DistanceToPolyline(mid, projected[i]).Dist;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }
            if (best < 0 || bestDist > hideDistM * 2)
                continue;
            (edges[best].HiddenTwins ??= new List<Coordinate[]>()).Add(piece.Geo);
        }
    }

    private static bool NamesCompatible(string a, string b) => a == null || b == null || a == b;

    private static NetworkEdgeDraft MakeEdge(NetworkEdgeDraft source, Coordinate[] geo, int start, int endExclusive)
    {
        if (endExclusive - start < 2)
            return null;
        var coords = geo[start..endExclusive];
        var lengthKm = NetworkGeo.LengthKm(coords);
        if (lengthKm < MinPieceKm)
            return null;
        return source.CopyMetadataTo(new NetworkEdgeDraft
        {
            Geometry = Factory.CreateLineString(coords),
            LengthKm = lengthKm,
        });
    }

    private static void SmoothRuns(bool[] state)
    {
        var i = 0;
        while (i < state.Length)
        {
            var j = i;
            while (j < state.Length && state[j] == state[i])
                j++;
            var interior = i > 0 && j < state.Length;
            if (interior && j - i < SmoothRunSamples && state[i - 1] == state[j])
            {
                for (var k = i; k < j; k++)
                    state[k] = state[i - 1];
            }
            i = j;
        }
    }

    private static void FlipInteriorVisibleRuns(bool[] shadow)
    {
        var i = 0;
        while (i < shadow.Length)
        {
            var j = i;
            while (j < shadow.Length && shadow[j] == shadow[i])
                j++;
            if (!shadow[i] && i > 0 && j < shadow.Length && j - i < VisibleFlickerSamples)
            {
                for (var k = i; k < j; k++)
                    shadow[k] = true;
            }
            i = j;
        }
    }

    private static (double Dist, Coordinate Point)? NearestAligned(Coordinate point, double bearing, Sampled other, double maxDist)
    {
        var (dist, seg, nearest) = DistanceToPolyline(point, other.Proj);
        if (dist > maxDist)
            return null;
        var otherBearing = SegmentBearing(other.Proj[seg], other.Proj[seg + 1]);
        if (BearingDiff(bearing, otherBearing) > BearingTolDeg)
            return null;
        return (dist, nearest);
    }

    internal static (double Dist, int Seg, Coordinate Nearest) DistanceToPolyline(Coordinate p, Coordinate[] coords)
    {
        var best = double.MaxValue;
        var bestSeg = 0;
        var bestPoint = coords[0];
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
                bestSeg = k - 1;
                bestPoint = new Coordinate(cx, cy);
            }
        }
        return (best, bestSeg, bestPoint);
    }

    private static double SegmentBearing(Coordinate a, Coordinate b) =>
        (Math.Atan2(b.Y - a.Y, b.X - a.X) * 180.0 / Math.PI + 360.0) % 180.0;

    private static double BearingDiff(double a, double b)
    {
        var d = Math.Abs(a - b) % 180.0;
        return Math.Min(d, 180.0 - d);
    }

    internal static Envelope EnvelopeOf(Coordinate[] coords)
    {
        var envelope = new Envelope(coords[0]);
        foreach (var c in coords)
            envelope.ExpandToInclude(c);
        return envelope;
    }

    /// <summary>
    /// Geographic and projected samples in lockstep plus the local bearing per sample. This is the
    /// dedup-internal per-segment sampler; it is NOT the coverage lattice (<see cref="NetworkLattice"/>).
    /// </summary>
    private static Sampled SampleAlongSegments(Coordinate[] geo, NetworkGeo.LocalProjection projection)
    {
        var proj = projection.Project(geo);
        var geoOut = new List<Coordinate>(geo.Length * 2) { geo[0] };
        var projOut = new List<Coordinate>(geo.Length * 2) { proj[0] };
        var bearings = new List<double>(geo.Length * 2) { SegmentBearing(proj[0], proj[Math.Min(1, proj.Length - 1)]) };
        for (var i = 1; i < geo.Length; i++)
        {
            var dist = proj[i - 1].Distance(proj[i]);
            var steps = Math.Max(1, (int)Math.Ceiling(dist / SampleSpacingM));
            var bearing = SegmentBearing(proj[i - 1], proj[i]);
            for (var s = 1; s <= steps; s++)
            {
                var t = (double)s / steps;
                geoOut.Add(new Coordinate(
                    geo[i - 1].X + (geo[i].X - geo[i - 1].X) * t,
                    geo[i - 1].Y + (geo[i].Y - geo[i - 1].Y) * t));
                projOut.Add(new Coordinate(
                    proj[i - 1].X + (proj[i].X - proj[i - 1].X) * t,
                    proj[i - 1].Y + (proj[i].Y - proj[i - 1].Y) * t));
                bearings.Add(bearing);
            }
        }
        return new Sampled { Geo = geoOut.ToArray(), Proj = projOut.ToArray(), BearingDeg = bearings.ToArray() };
    }
}
