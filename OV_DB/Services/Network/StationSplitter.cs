using System;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;

namespace OV_DB.Services.Network;

public sealed record NetworkStop(Coordinate Location, string Name);

/// <summary>
/// Splits edges at stations and stops so every edge — and so every uncovered run — carries a
/// human label ("Utrecht Centraal – Bunnik"), and so names stop bleeding through stations. This
/// is display structure only: coverage is per sample and never depended on these cuts (design
/// doc 4.6). Runs after the parallel dedup (stops snap onto the surviving corridor line) and
/// before the length cap.
/// </summary>
public static class StationSplitter
{
    /// <summary>Station nodes sit on platforms rather than track centerlines, and dedup may have averaged the line a few metres away.</summary>
    private const double SnapDistM = 75;
    /// <summary>No split this close to an edge end or another split: no slivers.</summary>
    private const double MinGapM = 100;

    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    public static List<NetworkEdgeDraft> Apply(List<NetworkEdgeDraft> edges, IReadOnlyList<NetworkStop> stops, NetworkGeo.LocalProjection projection)
    {
        if (stops.Count == 0 || edges.Count == 0)
            return edges;

        var projected = new Coordinate[edges.Count][];
        var tree = new STRtree<int>();
        for (var i = 0; i < edges.Count; i++)
        {
            projected[i] = projection.Project(edges[i].Geometry.Coordinates);
            tree.Insert(ParallelDedup.EnvelopeOf(projected[i]), i);
        }
        tree.Build();

        // A stop splits EVERY edge passing within SnapDistM (a shared corridor can carry two
        // surviving lines through one station), at the nearest point, recorded as chainage.
        var splits = new List<(double Chainage, string Name)>[edges.Count];
        foreach (var stop in stops)
        {
            var p = projection.Project(stop.Location);
            var envelope = new Envelope(p);
            envelope.ExpandBy(SnapDistM);
            foreach (var i in tree.Query(envelope))
            {
                var (dist, chainage) = NearestChainage(p, projected[i]);
                if (dist <= SnapDistM)
                    (splits[i] ??= new List<(double, string)>()).Add((chainage, stop.Name));
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
            var totalM = NetworkLattice.PolylineLength(projected[i]);
            var cuts = new List<(double Chainage, string Name)>();
            foreach (var split in splits[i].OrderBy(x => x.Chainage))
            {
                if (split.Chainage < MinGapM || split.Chainage > totalM - MinGapM)
                    continue;
                if (cuts.Count > 0 && split.Chainage - cuts[^1].Chainage < MinGapM)
                    continue;
                cuts.Add(split);
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

    private static void Cut(NetworkEdgeDraft edge, Coordinate[] proj, List<(double Chainage, string Name)> cuts, List<NetworkEdgeDraft> result)
    {
        var geo = edge.Geometry.Coordinates;
        var piece = new List<Coordinate> { geo[0] };
        var cum = 0d;
        var cutIdx = 0;
        var fromStation = edge.FromStation;
        for (var k = 1; k < geo.Length; k++)
        {
            var segLen = proj[k - 1].Distance(proj[k]);
            while (cutIdx < cuts.Count && segLen > 0 && cuts[cutIdx].Chainage <= cum + segLen)
            {
                var t = (cuts[cutIdx].Chainage - cum) / segLen;
                var cutPoint = new Coordinate(
                    geo[k - 1].X + (geo[k].X - geo[k - 1].X) * t,
                    geo[k - 1].Y + (geo[k].Y - geo[k - 1].Y) * t);
                piece.Add(cutPoint);
                Emit(edge, piece, fromStation, cuts[cutIdx].Name, result);
                fromStation = cuts[cutIdx].Name;
                piece = new List<Coordinate> { cutPoint };
                cutIdx++;
            }
            piece.Add(geo[k]);
            cum += segLen;
        }
        Emit(edge, piece, fromStation, edge.ToStation, result);
    }

    private static void Emit(NetworkEdgeDraft source, List<Coordinate> coords, string fromStation, string toStation, List<NetworkEdgeDraft> result)
    {
        if (coords.Count < 2)
            return;
        var lengthKm = NetworkGeo.LengthKm(coords);
        if (lengthKm <= 0)
            return;
        var piece = source.CopyMetadataTo(new NetworkEdgeDraft
        {
            Geometry = Factory.CreateLineString(coords.ToArray()),
            LengthKm = lengthKm,
        });
        piece.FromStation = fromStation;
        piece.ToStation = toStation;
        result.Add(piece);
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
