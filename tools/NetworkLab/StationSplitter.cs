using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;

namespace NetworkLab;

/// <summary>
/// Splits edges at stations, so that rides ending at a station end on an edge boundary instead
/// of leaving a half-covered edge below the coverage threshold (design doc 4.5b). Runs after the
/// parallel dedup (stations snap onto the surviving corridor line) and before chunking.
/// </summary>
public static class StationSplitter
{
    /// <summary>How far a station node may sit from the line it splits. Station nodes sit on
    /// platforms, not track centerlines, and dedup may have averaged the line a few metres away.</summary>
    private const double SnapDistM = 75;
    /// <summary>No split this close to an edge end or to another split - no slivers.</summary>
    private const double MinGapM = 100;

    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    public static List<NetworkEdgeDraft> Apply(List<NetworkEdgeDraft> edges, IReadOnlyList<Coordinate> stations, Geo.LocalProjection projection)
    {
        if (stations.Count == 0 || edges.Count == 0)
            return edges;

        var projected = new Coordinate[edges.Count][];
        var tree = new STRtree<int>();
        for (var i = 0; i < edges.Count; i++)
        {
            projected[i] = projection.Project(edges[i].Geometry.Coordinates);
            var envelope = new Envelope(projected[i][0]);
            foreach (var c in projected[i])
                envelope.ExpandToInclude(c);
            tree.Insert(envelope, i);
        }
        tree.Build();

        // A station splits EVERY edge passing within SnapDistM (a shared corridor can carry two
        // surviving lines through one station), at the nearest point, recorded as chainage.
        var splits = new List<double>[edges.Count];
        foreach (var station in stations)
        {
            var p = projection.Project(station);
            var envelope = new Envelope(p);
            envelope.ExpandBy(SnapDistM);
            foreach (var i in tree.Query(envelope))
            {
                var (dist, chainage) = NearestChainage(p, projected[i]);
                if (dist <= SnapDistM)
                    (splits[i] ??= new List<double>()).Add(chainage);
            }
        }

        var result = new List<NetworkEdgeDraft>(edges.Count);
        for (var i = 0; i < edges.Count; i++)
        {
            if (splits[i] == null)
            {
                result.Add(edges[i]);
                continue;
            }
            var totalM = PolylineLength(projected[i]);
            var cuts = new List<double>();
            foreach (var chainage in splits[i].OrderBy(x => x))
            {
                if (chainage < MinGapM || chainage > totalM - MinGapM)
                    continue;
                if (cuts.Count > 0 && chainage - cuts[^1] < MinGapM)
                    continue;
                cuts.Add(chainage);
            }
            if (cuts.Count == 0)
            {
                result.Add(edges[i]);
                continue;
            }
            Cut(edges[i], projected[i], cuts, result);
        }
        return result;
    }

    private static void Cut(NetworkEdgeDraft edge, Coordinate[] proj, List<double> cuts, List<NetworkEdgeDraft> result)
    {
        var geo = edge.Geometry.Coordinates;
        var piece = new List<Coordinate> { geo[0] };
        var cum = 0d;
        var cutIdx = 0;
        for (var k = 1; k < geo.Length; k++)
        {
            var segLen = proj[k - 1].Distance(proj[k]);
            while (cutIdx < cuts.Count && segLen > 0 && cuts[cutIdx] <= cum + segLen)
            {
                var t = (cuts[cutIdx] - cum) / segLen;
                var cutPoint = new Coordinate(
                    geo[k - 1].X + (geo[k].X - geo[k - 1].X) * t,
                    geo[k - 1].Y + (geo[k].Y - geo[k - 1].Y) * t);
                piece.Add(cutPoint);
                Emit(edge, piece, result);
                piece = new List<Coordinate> { cutPoint };
                cutIdx++;
            }
            piece.Add(geo[k]);
            cum += segLen;
        }
        Emit(edge, piece, result);
    }

    private static void Emit(NetworkEdgeDraft source, List<Coordinate> coords, List<NetworkEdgeDraft> result)
    {
        if (coords.Count < 2)
            return;
        var lengthKm = Geo.LengthKm(coords, 0, coords.Count);
        if (lengthKm <= 0)
            return;
        result.Add(new NetworkEdgeDraft
        {
            Geometry = Factory.CreateLineString(coords.ToArray()),
            LengthKm = lengthKm,
            IsPassenger = source.IsPassenger,
            Name = source.Name,
            OsmWayIds = source.OsmWayIds,
        });
    }

    private static double PolylineLength(Coordinate[] coords)
    {
        var total = 0d;
        for (var k = 1; k < coords.Length; k++)
            total += coords[k - 1].Distance(coords[k]);
        return total;
    }

    private static (double Dist, double Chainage) NearestChainage(Coordinate p, Coordinate[] coords)
    {
        var best = double.MaxValue;
        var bestChainage = 0d;
        var cum = 0d;
        for (var k = 1; k < coords.Length; k++)
        {
            var a = coords[k - 1];
            var b = coords[k];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var len2 = dx * dx + dy * dy;
            var segLen = Math.Sqrt(len2);
            var t = len2 <= 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2, 0, 1);
            var cx = a.X + t * dx;
            var cy = a.Y + t * dy;
            var d = Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy));
            if (d < best)
            {
                best = d;
                bestChainage = cum + t * segLen;
            }
            cum += segLen;
        }
        return (best, bestChainage);
    }
}
