using OV_DB.Services;
using OV_DB.Services.Network;
using Xunit;

namespace OV_DB.Tests;

/// <summary>How network assembly talks to Overpass: what counts as a usable answer, and how long to wait for one.</summary>
public class NetworkOverpassTests
{
    private const string Header = "{\"version\":0.6,\"osm3s\":{\"timestamp_osm_base\":\"2026-08-31T17:42:51Z\"},";

    [Fact]
    public void CompleteResponse_IsAccepted()
    {
        var body = Header + "\"elements\":[{\"type\":\"node\",\"id\":1,\"lat\":52,\"lon\":5}]}";
        Assert.True(RegionNetworkAssemblyService.IsCompleteResponse(body, out var problem));
        Assert.Null(problem);
    }

    [Fact]
    public void RuntimeErrorRemark_IsRejected()
    {
        // What Overpass sends, with HTTP 200, when a query times out halfway.
        var body = Header + "\"elements\":[{\"type\":\"node\",\"id\":1,\"lat\":52,\"lon\":5}],\n" +
                   "\"remark\": \"runtime error: Query timed out in \\\"query\\\" at line 1 after 901 seconds.\"}";
        Assert.False(RegionNetworkAssemblyService.IsCompleteResponse(body, out var problem));
        Assert.Contains("runtime error", problem);
    }

    [Fact]
    public void OutOfMemoryRemark_IsRejected()
    {
        var body = Header + "\"elements\":[],\"remark\": \"runtime error: Query run out of memory using about 2048 MB of RAM.\"}";
        Assert.False(RegionNetworkAssemblyService.IsCompleteResponse(body, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{\"elements\":[{\"type\":\"node\"")]   // cut off mid-transfer
    [InlineData("<html>504 Gateway Timeout</html>")]
    public void EmptyOrTruncatedResponse_IsRejected(string body) =>
        Assert.False(RegionNetworkAssemblyService.IsCompleteResponse(body, out _));

    [Fact]
    public void EdgeGeometry_IsCompactGeoJson()
    {
        var line = new NetTopologySuite.Geometries.GeometryFactory().CreateLineString(
        [
            new NetTopologySuite.Geometries.Coordinate(6.6943777, 53.3277493),
            new NetTopologySuite.Geometries.Coordinate(6.687383073175753, 53.32638270029634),
            new NetTopologySuite.Geometries.Coordinate(7, -0.5),
        ]);

        var json = NetworkGeo.ToGeoJson(line);

        Assert.Equal("{\"type\":\"LineString\",\"coordinates\":[[6.694378,53.327749],[6.687383,53.326383],[7,-0.5]]}", json);
        Assert.NotNull(Newtonsoft.Json.Linq.JObject.Parse(json));
    }

    [Fact]
    public void MissingRuns_GroupOnlyConnectedEdgesOfTheSameStretch()
    {
        static NetTopologySuite.Geometries.Coordinate C(double x, double y) => new(x, y);
        var edges = new[]
        {
            // "Spoor 6" at two different stations: same name, nothing in common.
            new MissingEdge(1, "Spoor 6", null, null, 1.0, C(5.0, 52.0), C(5.01, 52.0)),
            new MissingEdge(2, "Spoor 6", null, null, 2.0, C(6.0, 53.0), C(6.01, 53.0)),
            // A line between two stations in three connected pieces.
            new MissingEdge(3, "Zuiderzeelijn", "Lelystad", "Zwolle", 4.0, C(5.5, 52.5), C(5.6, 52.5)),
            new MissingEdge(4, "Zuiderzeelijn", "Lelystad", "Zwolle", 3.0, C(5.6, 52.5), C(5.7, 52.5)),
            new MissingEdge(5, "Zuiderzeelijn", "Lelystad", "Zwolle", 2.0, C(5.7, 52.5), C(5.8, 52.5)),
            // Unnamed, no stations, not touching anything.
            new MissingEdge(6, null, null, null, 0.7, C(4.0, 51.0), C(4.01, 51.0)),
            new MissingEdge(7, null, null, null, 0.2, C(4.5, 51.5), C(4.51, 51.5)),
        };

        var runs = MissingRuns.Group(edges, minKm: 0.5);

        Assert.Equal(4, runs.Count);                                  // edge 7 is below minKm
        Assert.Equal([3L, 4L, 5L], runs[0].EdgeIds.OrderBy(i => i));   // longest first
        Assert.Equal(9.0, runs[0].MissingKm);
        Assert.Equal(2, runs.Count(r => r.Name == "Spoor 6"));
        Assert.All(runs, r => Assert.Equal(r.Name == "Zuiderzeelijn" ? 3 : 1, r.EdgeIds.Length));
    }

    [Fact]
    public void AssemblyRequests_ForAWaitingRegion_Coalesce()
    {
        // Region ids far from real ones: the queue is static and shared with other tests.
        RegionNetworkAssemblyService.Enqueue(900_001, refreshFromOsm: false);
        RegionNetworkAssemblyService.Enqueue(900_001, refreshFromOsm: true);   // merges, refetch wins
        RegionNetworkAssemblyService.Enqueue(900_001, refreshFromOsm: false);
        RegionNetworkAssemblyService.Enqueue(900_002, refreshFromOsm: false);

        var seen = new System.Collections.Generic.List<NetworkAssemblyRequest>();
        while (RegionNetworkAssemblyService.TryDequeue(out var request))
            if (request.RegionId >= 900_001)
                seen.Add(request);

        Assert.Equal([new NetworkAssemblyRequest(900_001, true), new NetworkAssemblyRequest(900_002, false)], seen);

        // Once dequeued (running), a new request queues a fresh run.
        RegionNetworkAssemblyService.Enqueue(900_001, refreshFromOsm: false);
        Assert.True(RegionNetworkAssemblyService.TryDequeue(out var again));
        Assert.Equal(new NetworkAssemblyRequest(900_001, false), again);
    }

    [Theory]
    [InlineData("[out:json][timeout:900];area(3602323309)->.a;way(area.a);out;", OverpassService.LongQueryClientName)]
    [InlineData("[out:json][timeout:300];node(1);out;", OverpassService.LongQueryClientName)]
    [InlineData("[out:json][timeout:120];node(1);out;", "OSM")]
    [InlineData("[out:json][timeout:30];node(1);out;", "OSM")]
    [InlineData("[out:json];node(1);out;", "OSM")]
    public void QueryTimeout_PicksAClientThatWaitsLongEnough(string query, string expectedClient) =>
        Assert.Equal(expectedClient, OverpassService.ClientFor(query));
}
