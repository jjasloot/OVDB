using System;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using NetTopologySuite.Geometries.Utilities;
using OVDB_database.Models;

namespace OV_DB.Services.Network;

/// <summary>Everything the assembly needs besides the track itself.</summary>
public sealed class NetworkAssemblyInput
{
    public OsmRailData Track { get; init; }
    public MultiPolygon RegionGeometry { get; init; }
    /// <summary>Per OSM way id: which passenger services run over it (from route relations).</summary>
    public IReadOnlyDictionary<long, PtService> PtServices { get; init; } = new Dictionary<long, PtService>();
    public IReadOnlyDictionary<long, NetworkOverrideKind> Overrides { get; init; } = new Dictionary<long, NetworkOverrideKind>();
    public IReadOnlyList<NetworkStop> RailStations { get; init; } = Array.Empty<NetworkStop>();
    public IReadOnlyList<NetworkStop> UrbanStops { get; init; } = Array.Empty<NetworkStop>();
}

public sealed class NetworkAssemblyResult
{
    public Dictionary<NetworkMode, List<NetworkEdgeDraft>> Edges { get; } = new();
    public bool ClassifiedByRelations { get; set; }
    public NetworkGeo.LocalProjection Projection { get; set; }
}

/// <summary>
/// Turns raw OSM track into the deduplicated, station-split, length-capped edges of a region's
/// networks: classify → junctions → chain walk → clip → parallel dedup → split → cap → attach
/// twins. Pure in-memory geometry; no database. Validated algorithm by algorithm in
/// tools/NetworkLab (design doc section 4).
/// </summary>
public static class NetworkTopologyBuilder
{
    /// <summary>Bitmap-size cap, not a coverage semantic: 10 km = 401 lattice samples.</summary>
    public const double MaxEdgeKm = 10.0;
    public const double MinClippedEdgeKm = 0.1;
    /// <summary>Below this share of served ways the route relations are judged too sparse to classify by.</summary>
    public const double RelationCoverageFloor = 0.30;

    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    public static NetworkMode? ModeOf(string railway) => railway switch
    {
        "rail" or "narrow_gauge" or "preserved" => NetworkMode.Rail,
        "light_rail" or "subway" or "tram" => NetworkMode.UrbanRail,
        _ => null,
    };

    /// <summary>
    /// Fallback passenger heuristic for regions without usable route relations. Untagged ways
    /// count as passenger (OSM only reliably tags the exceptions); railway:traffic_mode catches
    /// lines like the Betuweroute that are usage=main but freight-only.
    /// </summary>
    public static bool IsPassengerByTags(RailWay way) =>
        way.Usage is not ("freight" or "industrial" or "military" or "test")
        && way.TrafficMode != "freight";

    internal sealed record ModeWay(RailWay Way, bool Passenger, NetworkOverrideKind? Override);

    public static NetworkAssemblyResult Build(NetworkAssemblyInput input, Action<int> progress = null)
    {
        progress ??= _ => { };
        var data = input.Track;
        // The EF-materialized region geometry carries the MySQL provider's factory, whose overlay
        // setting differs from ours; NTS refuses overlay operations across such factories.
        var region = input.RegionGeometry == null ? null : (MultiPolygon)Factory.CreateGeometry(input.RegionGeometry);

        var lat0 = region?.Centroid.Y ?? data.Nodes.Values.Select(n => n.Lat).DefaultIfEmpty(52).First();
        var result = new NetworkAssemblyResult { Projection = new NetworkGeo.LocalProjection(lat0) };

        // Classification (design 4.2): relations are the truth where they exist.
        var relevant = data.Ways.Where(w => ModeOf(w.Railway) != null).ToList();
        var served = relevant.Count(w => input.PtServices.ContainsKey(w.Id));
        var useRelations = relevant.Count > 0 && served >= RelationCoverageFloor * relevant.Count;
        result.ClassifiedByRelations = useRelations;

        var annotated = new List<(NetworkMode Mode, ModeWay Way)>(relevant.Count);
        foreach (var way in relevant)
        {
            var mode = ModeOf(way.Railway)!.Value;
            bool passenger;
            if (useRelations)
            {
                var pt = input.PtServices.TryGetValue(way.Id, out var p) ? p : PtService.None;
                passenger = pt != PtService.None;
                if (mode == NetworkMode.Rail && pt == PtService.Urban)
                    mode = NetworkMode.UrbanRail;
            }
            else
            {
                passenger = IsPassengerByTags(way);
            }

            NetworkOverrideKind? over = null;
            if (input.Overrides.TryGetValue(way.Id, out var kind))
            {
                over = kind;
                switch (kind)
                {
                    case NetworkOverrideKind.Excluded: continue;
                    case NetworkOverrideKind.ForceRail: mode = NetworkMode.Rail; break;
                    case NetworkOverrideKind.ForceUrbanRail: mode = NetworkMode.UrbanRail; break;
                }
            }
            annotated.Add((mode, new ModeWay(way, passenger, over)));
        }
        progress(10);

        foreach (var group in annotated.GroupBy(a => a.Mode))
        {
            var mode = group.Key;
            var edges = BuildMode(group.Select(a => a.Way).ToList(), data.Nodes);
            edges = Clip(edges, region);
            var hideDist = ParallelDedup.HideDistFor(mode);
            edges = ParallelDedup.Apply(edges, result.Projection, hideDist, out var shadowPieces);
            edges = StationSplitter.Apply(edges, mode == NetworkMode.Rail ? input.RailStations : input.UrbanStops, result.Projection);
            edges = Cap(edges);
            ParallelDedup.AttachTwins(edges, shadowPieces, result.Projection, hideDist);
            foreach (var edge in edges)
                edge.Mode = mode;
            result.Edges[mode] = edges;
            progress(mode == NetworkMode.Rail ? 55 : 90);
        }
        return result;
    }

    // ---- junctions and chain walk --------------------------------------------------------------

    private sealed record Segment(long[] NodeIds, int WayIndex);

    private static List<NetworkEdgeDraft> BuildMode(List<ModeWay> ways, Dictionary<long, (double Lon, double Lat)> nodes)
    {
        // A node used twice anywhere in this mode's ways is a topological meeting point.
        // Duplicates within a single way (balloon loops) count twice on purpose.
        var nodeUse = new Dictionary<long, int>();
        foreach (var way in ways)
            foreach (var nodeId in way.Way.NodeIds)
                nodeUse[nodeId] = nodeUse.TryGetValue(nodeId, out var n) ? n + 1 : 1;

        var segments = new List<Segment>();
        for (var w = 0; w < ways.Count; w++)
        {
            var ids = ways[w].Way.NodeIds;
            var start = 0;
            for (var i = 1; i < ids.Length - 1; i++)
            {
                if (nodeUse[ids[i]] >= 2)
                {
                    segments.Add(new Segment(ids[start..(i + 1)], w));
                    start = i;
                }
            }
            segments.Add(new Segment(ids[start..], w));
        }

        var byNode = new Dictionary<long, List<(int Seg, bool AtStart)>>();
        for (var s = 0; s < segments.Count; s++)
        {
            Add(byNode, segments[s].NodeIds[0], (s, true));
            Add(byNode, segments[s].NodeIds[^1], (s, false));
        }

        // Break nodes end an edge: junctions and dead ends, passenger-flag transitions (a line
        // continuing as freight-only must not merge through) and override boundaries.
        var isBreak = new Dictionary<long, bool>(byNode.Count);
        foreach (var (nodeId, ends) in byNode)
        {
            if (ends.Count != 2)
            {
                isBreak[nodeId] = true;
                continue;
            }
            var a = ways[segments[ends[0].Seg].WayIndex];
            var b = ways[segments[ends[1].Seg].WayIndex];
            isBreak[nodeId] = a.Passenger != b.Passenger || a.Override != b.Override;
        }

        var visited = new bool[segments.Count];
        var edges = new List<NetworkEdgeDraft>();
        foreach (var (nodeId, ends) in byNode)
        {
            if (!isBreak[nodeId])
                continue;
            foreach (var end in ends)
                if (!visited[end.Seg])
                    edges.Add(Walk(end.Seg, end.AtStart, segments, ways, nodes, byNode, isBreak, visited));
        }
        for (var s = 0; s < segments.Count; s++)
            if (!visited[s])
                edges.Add(Walk(s, true, segments, ways, nodes, byNode, isBreak, visited));

        return edges.Where(e => e != null).ToList();
    }

    private static void Add(Dictionary<long, List<(int, bool)>> byNode, long nodeId, (int, bool) end)
    {
        if (!byNode.TryGetValue(nodeId, out var list))
            byNode[nodeId] = list = new List<(int, bool)>(2);
        list.Add(end);
    }

    private static NetworkEdgeDraft Walk(int startSeg, bool enterAtStart, List<Segment> segments, List<ModeWay> ways,
        Dictionary<long, (double Lon, double Lat)> nodes, Dictionary<long, List<(int Seg, bool AtStart)>> byNode,
        Dictionary<long, bool> isBreak, bool[] visited)
    {
        var coords = new List<Coordinate>();
        var wayIds = new List<long>();
        var nameKm = new Dictionary<string, double>();
        var passenger = ways[segments[startSeg].WayIndex].Passenger;

        var seg = startSeg;
        var atStart = enterAtStart;
        while (true)
        {
            visited[seg] = true;
            var way = ways[segments[seg].WayIndex].Way;
            wayIds.Add(way.Id);

            var ids = segments[seg].NodeIds;
            var ordered = atStart ? ids : ids.Reverse().ToArray();
            var segStartCount = coords.Count;
            foreach (var nodeId in coords.Count == 0 ? ordered : ordered.Skip(1))
                if (nodes.TryGetValue(nodeId, out var c))
                    coords.Add(new Coordinate(c.Lon, c.Lat));
            if (way.Name != null)
            {
                var segKm = NetworkGeo.LengthKm(coords, Math.Max(segStartCount - 1, 0), coords.Count);
                nameKm[way.Name] = nameKm.TryGetValue(way.Name, out var km) ? km + segKm : segKm;
            }

            var exitNode = ordered[^1];
            if (isBreak[exitNode])
                break;
            var next = byNode[exitNode].First(e => e.Seg != seg || e.AtStart == atStart);
            if (visited[next.Seg])
                break;
            seg = next.Seg;
            atStart = next.AtStart;
        }

        if (coords.Count < 2)
            return null;
        var lengthKm = NetworkGeo.LengthKm(coords);
        if (lengthKm <= 0)
            return null;
        return new NetworkEdgeDraft
        {
            Geometry = Factory.CreateLineString(coords.ToArray()),
            LengthKm = lengthKm,
            IsPassenger = passenger,
            Name = nameKm.Count == 0 ? null : nameKm.MaxBy(kv => kv.Value).Key,
            OsmWayIds = wayIds,
        };
    }

    // ---- clip -----------------------------------------------------------------------------------

    private static List<NetworkEdgeDraft> Clip(List<NetworkEdgeDraft> edges, MultiPolygon region)
    {
        if (region == null)
            return edges;
        var prepared = PreparedGeometryFactory.Prepare(region);
        var result = new List<NetworkEdgeDraft>(edges.Count);
        foreach (var edge in edges)
        {
            if (prepared.Contains(edge.Geometry))
            {
                result.Add(edge);
                continue;
            }
            if (!prepared.Intersects(edge.Geometry))
                continue;
            var clipped = edge.Geometry.Intersection(region);
            foreach (var piece in LineStringExtracter.GetLines(clipped).Cast<LineString>())
            {
                var lengthKm = NetworkGeo.LengthKm(piece.Coordinates);
                if (lengthKm < MinClippedEdgeKm)
                    continue;
                result.Add(edge.CopyMetadataTo(new NetworkEdgeDraft { Geometry = piece, LengthKm = lengthKm }));
            }
        }
        return result;
    }

    // ---- length cap -----------------------------------------------------------------------------

    private static List<NetworkEdgeDraft> Cap(List<NetworkEdgeDraft> edges)
    {
        var result = new List<NetworkEdgeDraft>(edges.Count);
        foreach (var edge in edges)
        {
            if (edge.LengthKm <= MaxEdgeKm)
            {
                result.Add(edge);
                continue;
            }
            var parts = (int)Math.Ceiling(edge.LengthKm / MaxEdgeKm);
            var targetKm = edge.LengthKm / parts;
            var coords = edge.Geometry.Coordinates;
            var chunk = new List<Coordinate> { coords[0] };
            var chunkKm = 0d;
            var emitted = 0;
            var fromStation = edge.FromStation;
            for (var i = 1; i < coords.Length; i++)
            {
                var stepKm = NetworkGeo.HaversineKm(coords[i - 1], coords[i]);
                while (emitted < parts - 1 && chunkKm + stepKm >= targetKm)
                {
                    var need = targetKm - chunkKm;
                    var t = stepKm <= 0 ? 1 : need / stepKm;
                    var previous = chunk[^1];
                    var breakPoint = new Coordinate(previous.X + (coords[i].X - previous.X) * t, previous.Y + (coords[i].Y - previous.Y) * t);
                    chunk.Add(breakPoint);
                    result.Add(MakePiece(edge, chunk, fromStation, null));
                    fromStation = null;
                    emitted++;
                    chunk = new List<Coordinate> { breakPoint };
                    chunkKm = 0;
                    stepKm = NetworkGeo.HaversineKm(breakPoint, coords[i]);
                }
                chunk.Add(coords[i]);
                chunkKm += stepKm;
            }
            if (chunk.Count >= 2)
                result.Add(MakePiece(edge, chunk, fromStation, edge.ToStation));
        }
        return result;
    }

    private static NetworkEdgeDraft MakePiece(NetworkEdgeDraft source, List<Coordinate> coords, string fromStation, string toStation)
    {
        var piece = source.CopyMetadataTo(new NetworkEdgeDraft
        {
            Geometry = Factory.CreateLineString(coords.ToArray()),
            LengthKm = NetworkGeo.LengthKm(coords),
        });
        piece.FromStation = fromStation;
        piece.ToStation = toStation;
        return piece;
    }
}
