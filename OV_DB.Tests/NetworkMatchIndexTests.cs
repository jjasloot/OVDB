using System.Linq;
using NetTopologySuite.Geometries;
using OV_DB.Services.Network;
using Xunit;

namespace OV_DB.Tests;

/// <summary>Matching core against synthetic edges near 52°N 5°E (design doc section 8).</summary>
public class NetworkMatchIndexTests
{
    private const double DegPerMetreLat = 1 / 111_320.0;
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);
    private static readonly NetworkGeo.LocalProjection Projection = new(52.0);

    private static LineString Line(params (double Lon, double Lat)[] points) =>
        Factory.CreateLineString(points.Select(p => new Coordinate(p.Lon, p.Lat)).ToArray());

    /// <summary>A 2 km straight edge along 52.0°N, with the stored sample count assembly would compute.</summary>
    private static MatchableEdge Edge(long id, double latOffsetM = 0, MultiLineString twins = null)
    {
        var geometry = Line((5.00, 52.0 + latOffsetM * DegPerMetreLat), (5.0292, 52.0 + latOffsetM * DegPerMetreLat));
        var count = NetworkLattice.SampleCount(NetworkLattice.PolylineLength(Projection.Project(geometry.Coordinates)));
        return new MatchableEdge(id, geometry, count, twins);
    }

    [Fact]
    public void RouteOnTheTrack_CoversEveryBit()
    {
        var edge = Edge(1);
        var index = new NetworkMatchIndex([edge], Projection);

        var result = index.Match(Line((4.99, 52.0 + 5 * DegPerMetreLat), (5.04, 52.0 + 5 * DegPerMetreLat)));

        Assert.True(result.ContainsKey(1));
        Assert.Equal(edge.SampleCount, result[1].Length);
        Assert.All(result[1], Assert.True);
    }

    [Fact]
    public void RouteFortyMetresAway_MatchesNothing()
    {
        var index = new NetworkMatchIndex([Edge(1)], Projection);

        var result = index.Match(Line((4.99, 52.0 + 40 * DegPerMetreLat), (5.04, 52.0 + 40 * DegPerMetreLat)));

        Assert.Empty(result);
    }

    [Fact]
    public void PerpendicularCrossing_StoresNoRow()
    {
        var index = new NetworkMatchIndex([Edge(1)], Projection);

        var result = index.Match(Line((5.015, 51.99), (5.015, 52.01)));

        Assert.Empty(result);
    }

    [Fact]
    public void HalfRide_CoversHalfTheBits()
    {
        var edge = Edge(1);
        var index = new NetworkMatchIndex([edge], Projection);

        var result = index.Match(Line((4.99, 52.0), (5.0146, 52.0)));   // ends at the edge's midpoint

        var covered = result[1].Count(b => b);
        Assert.InRange(covered, edge.SampleCount * 0.45, edge.SampleCount * 0.55);
        Assert.True(result[1][0]);
        Assert.False(result[1][^1]);
    }

    [Fact]
    public void RideOnTheHiddenTwin_CoversTheCorridor()
    {
        // The survivor line is 30 m north of the hidden physical track; the ride follows the
        // hidden track. Without probes it would sit beyond the 15 m tolerance.
        var hidden = Line((5.00, 52.0), (5.0292, 52.0));
        var edge = Edge(1, latOffsetM: 30, twins: Factory.CreateMultiLineString([hidden]));
        var index = new NetworkMatchIndex([edge], Projection);

        var result = index.Match(Line((4.99, 52.0), (5.04, 52.0)));

        Assert.True(result.ContainsKey(1));
        Assert.All(result[1], Assert.True);
    }

    [Fact]
    public void ShortConnectorRiddenEndToEnd_IsCovered()
    {
        // A 30 m junction connector has fewer lattice samples than MinStoredSamples; riding over
        // it must still cover it, or completion can never reach 100%.
        var geometry = Line((5.00, 52.0), (5.00044, 52.0));
        var count = NetworkLattice.SampleCount(NetworkLattice.PolylineLength(Projection.Project(geometry.Coordinates)));
        Assert.True(count < NetworkMatchIndex.MinStoredSamples);
        var index = new NetworkMatchIndex([new MatchableEdge(7, geometry, count, null)], Projection);

        var result = index.Match(Line((4.99, 52.0), (5.01, 52.0)));

        Assert.True(result.ContainsKey(7));
        Assert.All(result[7], Assert.True);
    }

    [Theory]
    [InlineData(4, 80, true)]    // the normal threshold
    [InlineData(3, 80, false)]   // a crossing on a long edge
    [InlineData(2, 2, true)]     // a short edge ridden end to end
    [InlineData(1, 2, false)]    // a short edge only touched
    [InlineData(1, 1, true)]     // a degenerate one-sample edge
    [InlineData(0, 1, false)]
    public void IsStored_RequiresEnoughEvidence(int covered, int sampleCount, bool expected) =>
        Assert.Equal(expected, NetworkMatchIndex.IsStored(covered, sampleCount));

    [Fact]
    public void TwoRoutes_UnionAcrossBitmaps()
    {
        var edge = Edge(1);
        var index = new NetworkMatchIndex([edge], Projection);

        var first = index.Match(Line((4.99, 52.0), (5.0146, 52.0)))[1];
        var second = index.Match(Line((5.0146, 52.0), (5.04, 52.0)))[1];

        var union = new bool[edge.SampleCount];
        NetworkLattice.OrInto(union, NetworkLattice.ToBytes(first));
        NetworkLattice.OrInto(union, NetworkLattice.ToBytes(second));
        Assert.All(union, Assert.True);
        Assert.Single(NetworkLattice.RunsToIntervals(union));
    }
}
