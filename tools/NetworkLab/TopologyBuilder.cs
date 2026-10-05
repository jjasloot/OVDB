using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using NetTopologySuite.Geometries.Utilities;

namespace NetworkLab;

// Two networks only: heavy rail, and everything urban. The tram/light_rail/subway distinction
// is hopeless in OSM (Ruhrgebiet Stadtbahn is all three at once) and users don't think in it.
public enum NetworkMode { Rail, UrbanRail }

public sealed class NetworkEdgeDraft
{
    public NetworkMode Mode;
    public LineString Geometry;
    public double LengthKm;
    public bool IsPassenger;
    public string Name;
    public List<long> OsmWayIds = new();

    /// <summary>
    /// Physical track geometries hidden by the parallel dedup and attached to this surviving
    /// edge. The matcher probes them too, so riding either physical track of the corridor
    /// covers the corridor regardless of track spacing.
    /// </summary>
    public List<Coordinate[]> HiddenTwins;

    /// <summary>Union covered fraction over all matched routes, one slot per tolerance (Matcher.Tolerances).
    /// Zero when fewer than Matcher.MinStoredSamples samples are covered (storage floor).</summary>
    public double[] BestFraction;
    /// <summary>Union covered sample count per tolerance.</summary>
    public int[] CoveredCounts;
    /// <summary>Covered runs per tolerance as [startFraction, endFraction] pairs of edge length —
    /// what the completion endpoint will return for partial rendering.</summary>
    public List<double[]>[] Intervals;
}

public static class TopologyBuilder
{
    public const double MaxEdgeKm = 5.0;
    public const double MinClippedEdgeKm = 0.1;

    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    public static NetworkMode? ModeOf(string railway) => railway switch
    {
        "rail" or "narrow_gauge" or "preserved" => NetworkMode.Rail,
        "light_rail" or "subway" or "tram" => NetworkMode.UrbanRail,
        _ => null,
    };

    /// <summary>
    /// Passenger heuristic. Untagged ways count as passenger: OSM only reliably tags the
    /// exceptional usages. railway:traffic_mode catches lines like the Betuweroute that are
    /// usage=main but freight-only.
    /// </summary>
    public static bool IsPassenger(RailWay way) =>
        way.Usage is not ("freight" or "industrial" or "military" or "test")
        && way.TrafficMode != "freight";

    internal sealed record ModeWay(RailWay Way, bool Passenger);

    public static Dictionary<NetworkMode, List<NetworkEdgeDraft>> Build(OsmRailData data, MultiPolygon regionGeometry, Geo.LocalProjection projection,
        IReadOnlyList<Coordinate> railStations = null, IReadOnlyList<Coordinate> urbanStops = null,
        IReadOnlyDictionary<long, PtService> ptServices = null)
    {
        // The EF-materialized region geometry carries the MySQL provider's factory, whose
        // GeometryOverlay differs from ours; NTS refuses overlay ops across such factories.
        if (regionGeometry != null)
            regionGeometry = (MultiPolygon)Factory.CreateGeometry(regionGeometry);

        // With route relations available, service is the truth: a way is passenger track iff a
        // passenger route runs over it (untagged freight lines and draisines demote gracefully
        // to bonus), and a rail-tagged way served only by urban routes belongs to UrbanRail
        // (Köln line 7 to Benzelrath on the ex-KFBE). Without relations, the tag heuristic.
        var useRelations = ptServices is { Count: > 0 };
        var annotated = new List<(NetworkMode Mode, ModeWay Way)>(data.Ways.Count);
        foreach (var way in data.Ways)
        {
            if (ModeOf(way.Railway) is not { } mode)
                continue;
            bool passenger;
            if (useRelations)
            {
                var pt = ptServices.GetValueOrDefault(way.Id, PtService.None);
                passenger = pt != PtService.None;
                if (mode == NetworkMode.Rail && pt == PtService.Urban)
                    mode = NetworkMode.UrbanRail;
            }
            else
            {
                passenger = IsPassenger(way);
            }
            annotated.Add((mode, new ModeWay(way, passenger)));
        }

        var result = new Dictionary<NetworkMode, List<NetworkEdgeDraft>>();
        foreach (var group in annotated.GroupBy(a => a.Mode))
        {
            var mode = group.Key;
            var edges = BuildMode(group.Select(a => a.Way).ToList(), data.Nodes);
            edges = Clip(edges, regionGeometry);
            // Street-tram direction pairs run further apart than rail twins, so urban rail hides
            // a wider corridor; centerline averaging stays at 15 m for both (ParallelDedup).
            var hideDistM = mode == NetworkMode.UrbanRail ? 35.0 : 25.0;
            edges = ParallelDedup.Apply(edges, projection, hideDistM, out var shadowPieces);
            // Rail splits at the DB's train stations; UrbanRail at OSM tram stops and metro
            // stations (the Stations table deliberately excludes subway and tram).
            edges = StationSplitter.Apply(edges, (mode == NetworkMode.Rail ? railStations : urbanStops) ?? [], projection);
            edges = Chunk(edges);
            ParallelDedup.AttachTwins(edges, shadowPieces, projection, hideDistM);
            foreach (var edge in edges)
            {
                edge.Mode = mode;
                edge.BestFraction = new double[Matcher.Tolerances.Length];
            }
            result[mode] = edges;
        }
        return result;
    }

    // ---- Junction detection + primitive segments -------------------------------------------

    private sealed record Segment(long[] NodeIds, int WayIndex);

    private static List<NetworkEdgeDraft> BuildMode(List<ModeWay> ways, Dictionary<long, (double Lon, double Lat)> nodes)
    {
        // A node used twice anywhere in this mode's ways is a topological meeting point.
        // Duplicates within a single way (balloon loops) count twice on purpose.
        var nodeUse = new Dictionary<long, int>();
        foreach (var way in ways)
            foreach (var nodeId in way.Way.NodeIds)
                nodeUse[nodeId] = nodeUse.TryGetValue(nodeId, out var n) ? n + 1 : 1;

        // Split every way at interior meeting points into primitive segments.
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

        // Endpoint adjacency. (segmentIndex, atStart) pairs per node.
        var byNode = new Dictionary<long, List<(int Seg, bool AtStart)>>();
        for (var s = 0; s < segments.Count; s++)
        {
            Add(byNode, segments[s].NodeIds[0], (s, true));
            Add(byNode, segments[s].NodeIds[^1], (s, false));
        }

        // Break nodes end an edge: junctions/dead ends (degree != 2), and passenger-flag
        // transitions (a line continuing as freight-only must not merge through).
        var isBreak = new Dictionary<long, bool>(byNode.Count);
        foreach (var (nodeId, ends) in byNode)
        {
            isBreak[nodeId] = ends.Count != 2
                || ways[segments[ends[0].Seg].WayIndex].Passenger != ways[segments[ends[1].Seg].WayIndex].Passenger;
        }

        // Chain walk: start at every break-node end, then sweep up pure cycles.
        var visited = new bool[segments.Count];
        var edges = new List<NetworkEdgeDraft>();
        foreach (var (nodeId, ends) in byNode)
        {
            if (!isBreak[nodeId])
                continue;
            foreach (var end in ends)
            {
                if (!visited[end.Seg])
                    edges.Add(Walk(end.Seg, end.AtStart, segments, ways, nodes, byNode, isBreak, visited));
            }
        }
        for (var s = 0; s < segments.Count; s++)
        {
            if (!visited[s])
                edges.Add(Walk(s, enterAtStart: true, segments, ways, nodes, byNode, isBreak, visited));
        }

        return edges.Where(e => e != null).ToList();
    }

    private static void Add(Dictionary<long, List<(int, bool)>> byNode, long nodeId, (int, bool) end)
    {
        if (!byNode.TryGetValue(nodeId, out var list))
            byNode[nodeId] = list = new List<(int, bool)>(2);
        list.Add(end);
    }

    // ---- Chain walk into maximal edges -----------------------------------------------------

    private static NetworkEdgeDraft Walk(
        int startSeg, bool enterAtStart,
        List<Segment> segments, List<ModeWay> ways, Dictionary<long, (double Lon, double Lat)> nodes,
        Dictionary<long, List<(int Seg, bool AtStart)>> byNode, Dictionary<long, bool> isBreak,
        bool[] visited)
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
            foreach (var nodeId in (coords.Count == 0 ? ordered : ordered.Skip(1)))
            {
                if (nodes.TryGetValue(nodeId, out var c))
                    coords.Add(new Coordinate(c.Lon, c.Lat));
            }
            if (way.Name != null)
            {
                var segKm = Geo.LengthKm(coords, Math.Max(segStartCount - 1, 0), coords.Count);
                nameKm[way.Name] = nameKm.TryGetValue(way.Name, out var km) ? km + segKm : segKm;
            }

            var exitNode = ordered[^1];
            if (isBreak[exitNode])
                break;
            var next = byNode[exitNode].First(e => e.Seg != seg || e.AtStart == atStart);
            if (visited[next.Seg])
                break; // closed a cycle
            seg = next.Seg;
            atStart = next.AtStart;
        }

        if (coords.Count < 2)
            return null;
        var lengthKm = Geo.LengthKm(coords, 0, coords.Count);
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

    // ---- Clip to the region polygon ----------------------------------------------------------

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
                var lengthKm = Geo.LengthKm(piece.Coordinates, 0, piece.Coordinates.Length);
                if (lengthKm < MinClippedEdgeKm)
                    continue;
                result.Add(new NetworkEdgeDraft
                {
                    Geometry = piece,
                    LengthKm = lengthKm,
                    IsPassenger = edge.IsPassenger,
                    Name = edge.Name,
                    OsmWayIds = edge.OsmWayIds,
                });
            }
        }
        return result;
    }

    // ---- Chunk long edges ---------------------------------------------------------------------

    private static List<NetworkEdgeDraft> Chunk(List<NetworkEdgeDraft> edges)
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
            for (var i = 1; i < coords.Length; i++)
            {
                var stepKm = Geo.HaversineKm(coords[i - 1], coords[i]);
                // Emit break points while the current step crosses (possibly multiple) targets.
                while (emitted < parts - 1 && chunkKm + stepKm >= targetKm)
                {
                    var need = targetKm - chunkKm;
                    var t = stepKm <= 0 ? 1 : need / stepKm;
                    var previous = chunk[^1];
                    var breakPoint = new Coordinate(
                        previous.X + (coords[i].X - previous.X) * t,
                        previous.Y + (coords[i].Y - previous.Y) * t);
                    chunk.Add(breakPoint);
                    result.Add(MakeChunk(edge, chunk));
                    emitted++;
                    chunk = new List<Coordinate> { breakPoint };
                    chunkKm = 0;
                    stepKm = Geo.HaversineKm(breakPoint, coords[i]);
                }
                chunk.Add(coords[i]);
                chunkKm += stepKm;
            }
            if (chunk.Count >= 2)
                result.Add(MakeChunk(edge, chunk));
        }
        return result;
    }

    private static NetworkEdgeDraft MakeChunk(NetworkEdgeDraft edge, List<Coordinate> coords) => new()
    {
        Geometry = Factory.CreateLineString(coords.ToArray()),
        LengthKm = Geo.LengthKm(coords, 0, coords.Count),
        IsPassenger = edge.IsPassenger,
        Name = edge.Name,
        OsmWayIds = edge.OsmWayIds,
    };
}
