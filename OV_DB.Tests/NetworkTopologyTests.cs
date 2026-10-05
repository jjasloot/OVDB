using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Geometries;
using OV_DB.Services.Network;
using OVDB_database.Models;
using Xunit;

namespace OV_DB.Tests;

/// <summary>
/// Pure-function regression harness for the network assembly algorithms as ported from
/// tools/NetworkLab (design doc section 8). No database: synthetic ways near 52°N 5°E.
/// </summary>
public class NetworkTopologyTests
{
    private const double DegPerMetreLat = 1 / 111_320.0;

    private static OsmRailData Data(params (long Id, (double Lon, double Lat)[] Nodes)[] ways)
    {
        var data = new OsmRailData();
        long nextNode = 1;
        foreach (var (id, nodes) in ways)
        {
            var ids = new long[nodes.Length];
            for (var i = 0; i < nodes.Length; i++)
            {
                // Identical coordinates share a node id: that is how OSM expresses a junction.
                var existing = data.Nodes.FirstOrDefault(n => n.Value.Lon == nodes[i].Lon && n.Value.Lat == nodes[i].Lat);
                if (existing.Key != 0)
                {
                    ids[i] = existing.Key;
                }
                else
                {
                    ids[i] = nextNode++;
                    data.Nodes[ids[i]] = nodes[i];
                }
            }
            data.Ways.Add(new RailWay(id, ids, "rail", null, null, null));
        }
        return data;
    }

    private static List<NetworkEdgeDraft> BuildRail(OsmRailData data) =>
        NetworkTopologyBuilder.Build(new NetworkAssemblyInput { Track = data }).Edges.GetValueOrDefault(NetworkMode.Rail) ?? new();

    [Fact]
    public void ThreeWaysEndToEnd_MergeIntoOneEdge()
    {
        var edges = BuildRail(Data(
            (1, new[] { (5.00, 52.0), (5.01, 52.0) }),
            (2, new[] { (5.01, 52.0), (5.02, 52.0) }),
            (3, new[] { (5.02, 52.0), (5.03, 52.0) })));

        Assert.Single(edges);
        Assert.Equal(3, edges[0].OsmWayIds.Count);
        Assert.InRange(edges[0].LengthKm, 2.0, 2.1);
    }

    [Fact]
    public void TJunction_ProducesThreeEdges()
    {
        var edges = BuildRail(Data(
            (1, new[] { (5.00, 52.0), (5.01, 52.0), (5.02, 52.0) }),
            (2, new[] { (5.01, 52.0), (5.01, 52.01) })));

        Assert.Equal(3, edges.Count);
    }

    [Fact]
    public void WayEndTouchingInterior_SplitsTheInteriorWay()
    {
        // Way 2 ends on an interior node of way 1 without sharing an endpoint.
        var edges = BuildRail(Data(
            (1, new[] { (5.00, 52.0), (5.01, 52.0), (5.02, 52.0), (5.03, 52.0) }),
            (2, new[] { (5.02, 52.01), (5.02, 52.0) })));

        Assert.Equal(3, edges.Count);
    }

    [Fact]
    public void FreightTransition_IsABreakNode()
    {
        var data = new OsmRailData();
        data.Nodes[1] = (5.00, 52.0);
        data.Nodes[2] = (5.01, 52.0);
        data.Nodes[3] = (5.02, 52.0);
        data.Ways.Add(new RailWay(1, [1, 2], "rail", null, null, null));
        data.Ways.Add(new RailWay(2, [2, 3], "rail", "freight", null, null));

        var edges = BuildRail(data);

        Assert.Equal(2, edges.Count);
        Assert.Single(edges, e => e.IsPassenger);
        Assert.Single(edges, e => !e.IsPassenger);
    }

    [Fact]
    public void RelationsClassify_WhenCoverageIsHighEnough()
    {
        var data = Data(
            (1, new[] { (5.00, 52.0), (5.01, 52.0) }),
            (2, new[] { (5.02, 52.0), (5.03, 52.0) }),
            (3, new[] { (5.04, 52.0), (5.05, 52.0) }));
        var result = NetworkTopologyBuilder.Build(new NetworkAssemblyInput
        {
            Track = data,
            PtServices = new Dictionary<long, PtService> { [1] = PtService.Train, [2] = PtService.Urban },
        });

        Assert.True(result.ClassifiedByRelations);
        var rail = result.Edges[NetworkMode.Rail];
        Assert.Equal(2, rail.Count);                                  // way 1 (served) and way 3 (unserved, bonus)
        Assert.Single(rail, e => e.IsPassenger);
        Assert.Single(result.Edges[NetworkMode.UrbanRail]);           // rail track served only by urban routes moves
    }

    [Fact]
    public void Override_ExcludesAndForces()
    {
        var data = Data(
            (1, new[] { (5.00, 52.0), (5.01, 52.0) }),
            (2, new[] { (5.02, 52.0), (5.03, 52.0) }));
        var result = NetworkTopologyBuilder.Build(new NetworkAssemblyInput
        {
            Track = data,
            Overrides = new Dictionary<long, NetworkOverrideKind>
            {
                [1] = NetworkOverrideKind.Excluded,
                [2] = NetworkOverrideKind.ForceUrbanRail,
            },
        });

        Assert.False(result.Edges.ContainsKey(NetworkMode.Rail));
        Assert.Single(result.Edges[NetworkMode.UrbanRail]);
    }

    [Fact]
    public void ParallelTracks_CollapseToOneEdgeWithAHiddenTwin()
    {
        var offset = 6 * DegPerMetreLat;
        var edges = BuildRail(Data(
            (1, new[] { (5.00, 52.0), (5.01, 52.0), (5.02, 52.0), (5.03, 52.0) }),
            (2, new[] { (5.00, 52.0 + offset), (5.01, 52.0 + offset), (5.02, 52.0 + offset), (5.03, 52.0 + offset) })));

        Assert.Single(edges);
        Assert.NotNull(edges[0].HiddenTwins);
        Assert.Single(edges[0].HiddenTwins);
        // Centerline averaging: the survivor sits between the two tracks, not on either.
        var midLat = edges[0].Geometry.Coordinates[edges[0].Geometry.Coordinates.Length / 2].Y;
        Assert.InRange(midLat, 52.0 + offset * 0.3, 52.0 + offset * 0.7);
    }

    [Fact]
    public void DistinctNamedLines_StaySeparate()
    {
        var data = Data(
            (1, new[] { (5.00, 52.0), (5.01, 52.0), (5.02, 52.0) }),
            (2, new[] { (5.00, 52.0 + 20 * DegPerMetreLat), (5.01, 52.0 + 20 * DegPerMetreLat), (5.02, 52.0 + 20 * DegPerMetreLat) }));
        data.Ways[0] = data.Ways[0] with { Name = "Mainline" };
        data.Ways[1] = data.Ways[1] with { Name = "S-Bahn" };

        Assert.Equal(2, BuildRail(data).Count);
    }

    [Fact]
    public void StationSplit_LabelsBothSides()
    {
        var data = Data((1, new[] { (5.00, 52.0), (5.03, 52.0) }));
        var result = NetworkTopologyBuilder.Build(new NetworkAssemblyInput
        {
            Track = data,
            RailStations = [new NetworkStop(new Coordinate(5.015, 52.0 + 20 * DegPerMetreLat), "Halfway")],
        });

        var edges = result.Edges[NetworkMode.Rail];
        Assert.Equal(2, edges.Count);
        Assert.Contains(edges, e => e.ToStation == "Halfway" && e.FromStation == null);
        Assert.Contains(edges, e => e.FromStation == "Halfway" && e.ToStation == null);
    }

    [Fact]
    public void LengthCap_PreservesTotalLength()
    {
        // ~25 km straight line → 3 pieces of ~8.3 km.
        var edges = BuildRail(Data((1, new[] { (5.00, 52.0), (5.365, 52.0) })));

        Assert.Equal(3, edges.Count);
        Assert.All(edges, e => Assert.True(e.LengthKm <= NetworkTopologyBuilder.MaxEdgeKm));
        Assert.InRange(edges.Sum(e => e.LengthKm), 24.9, 25.1);
    }

    [Theory]
    [InlineData(100, 5)]   // 0, 25, 50, 75, 100
    [InlineData(125, 6)]
    [InlineData(137, 7)]   // 0..125 plus the end sample at 137
    [InlineData(10, 2)]    // 0 plus the end sample
    public void Lattice_SampleCountMatchesSamples(double lengthM, int expected)
    {
        var line = new[] { new Coordinate(0, 0), new Coordinate(lengthM * 0.4, 0), new Coordinate(lengthM, 0) };

        Assert.Equal(expected, NetworkLattice.SampleCount(lengthM));
        Assert.Equal(expected, NetworkLattice.Sample(line).Length);
    }

    [Fact]
    public void Intervals_FromRuns_AreFractionsWithHalfSpacingMargins()
    {
        var bits = new[] { true, true, true, true, false, false, false, false, true, true, true }; // 11 samples, L = 250 m
        var intervals = NetworkLattice.RunsToIntervals(bits, 250);

        Assert.Equal(2, intervals.Count);
        Assert.Equal(0.0, intervals[0][0], 3);
        Assert.Equal(0.35, intervals[0][1], 3);   // (75 + 12.5) / 250
        Assert.Equal(0.75, intervals[1][0], 3);   // (200 − 12.5) / 250
        Assert.Equal(1.0, intervals[1][1], 3);

        var full = new[] { true, true, true, true, true, true, true, true, true, true, true };
        var whole = NetworkLattice.RunsToIntervals(full, 250);
        Assert.Single(whole);
        Assert.Equal(0.0, whole[0][0], 3);
        Assert.Equal(1.0, whole[0][1], 3);
    }

    [Fact]
    public void Bitmaps_RoundTrip()
    {
        var bits = Enumerable.Range(0, 37).Select(i => i % 3 == 0).ToArray();
        var bytes = NetworkLattice.ToBytes(bits);

        Assert.Equal(5, bytes.Length);
        Assert.Equal(bits, NetworkLattice.ToBits(bytes, 37));
        Assert.Equal(13, NetworkLattice.PopCount(bits));
    }

    [Fact]
    public void SimplifiedChainage_MapsThroughKeptVertices()
    {
        // Full line: four vertices; the simplified line drops the middle two. Full chainage is
        // 100 + 100 + 100 m; the simplified line is a straight 300 m... except the kept-vertex
        // mapping is what guarantees the endpoints and the interior proportion line up.
        var full = new[] { new Coordinate(5.0, 52.0), new Coordinate(5.001, 52.0), new Coordinate(5.002, 52.0), new Coordinate(5.003, 52.0) };
        var simplified = new[] { full[0], full[3] };
        var map = new SimplifiedChainage(full, simplified);

        Assert.Equal(0.0, map.ToSimplifiedFraction(0.0), 6);
        Assert.Equal(1.0, map.ToSimplifiedFraction(1.0), 6);
        Assert.Equal(0.5, map.ToSimplifiedFraction(0.5), 3);
    }
}
