using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using OV_DB.Services;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Tests;

// The matcher is the one place inference happens, so what it will and will not claim is worth
// pinning down: the strength it assigns, the distances it accepts, whose data it can see, and -
// most of all - that it proposes rather than records.
public class StationTripMatcherTests
{
    // Utrecht Centraal, give or take. Real coordinates because the distance maths is latitude
    // dependent and a made-up equator location would not exercise it.
    private const double BaseLat = 52.0894;
    private const double BaseLon = 5.1100;

    private static OVDBDatabaseContext NewContext()
    {
        var options = new DbContextOptionsBuilder<OVDBDatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OVDBDatabaseContext(options);
    }

    private static StationTripMatcher NewMatcher(OVDBDatabaseContext context) =>
        new(context, new MatcherIndexCache());

    /// <summary>Metres north/east of the base point, so test intent reads in metres.</summary>
    private static (double Lat, double Lon) Offset(double northMetres, double eastMetres)
    {
        var lat = BaseLat + northMetres / 111_320.0;
        var lon = BaseLon + eastMetres / (111_320.0 * Math.Cos(BaseLat * Math.PI / 180.0));
        return (lat, lon);
    }

    private static LineString EastWestLineThroughBase(double lengthMetres = 20_000)
    {
        var west = Offset(0, -lengthMetres / 2);
        var east = Offset(0, lengthMetres / 2);
        return new LineString([new Coordinate(west.Lon, west.Lat), new Coordinate(east.Lon, east.Lat)]);
    }

    private static async Task<OVDBDatabaseContext> SeedAsync(
        LineString line = null,
        string from = "Somewhere",
        string to = "Elsewhere",
        int ownerUserId = 1,
        DateTime[] dates = null)
    {
        var context = NewContext();
        context.Maps.Add(new Map { MapId = 1, UserId = ownerUserId, Name = "Trains", MapGuid = Guid.NewGuid() });
        context.Routes.Add(new Route
        {
            RouteId = 1,
            Name = "The line",
            From = from,
            To = to,
            Share = Guid.NewGuid(),
            LineString = line ?? EastWestLineThroughBase()
        });
        context.RoutesMaps.Add(new RouteMap { RouteMapId = 1, RouteId = 1, MapId = 1 });

        var instanceDates = dates ?? [new DateTime(2021, 6, 1)];
        for (var i = 0; i < instanceDates.Length; i++)
        {
            context.RouteInstances.Add(new RouteInstance
            {
                RouteInstanceId = i + 1,
                RouteId = 1,
                Date = instanceDates[i]
            });
        }

        await context.SaveChangesAsync();
        return context;
    }

    private static async Task AddStationAsync(OVDBDatabaseContext context, int id, string name, double lat, double lon, bool hidden = false)
    {
        context.Stations.Add(new Station { Id = id, Name = name, Lattitude = lat, Longitude = lon, Hidden = hidden });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task StationOnTheLineIsExplainedByProximity()
    {
        using var context = await SeedAsync();
        await AddStationAsync(context, 10, "Halt", BaseLat, BaseLon);

        var candidates = await NewMatcher(context).FindTripsForStationAsync(1, 10);

        var candidate = Assert.Single(candidates);
        Assert.Equal(VisitEvidence.Proximity, candidate.Evidence);
        Assert.True(candidate.DistanceMetres < 1, $"expected the station to sit on the line, was {candidate.DistanceMetres}m");
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(280, true)]
    [InlineData(320, false)]
    [InlineData(2000, false)]
    public async Task ProximityIsJudgedInMetresNotDegrees(double metresNorth, bool expectedToMatch)
    {
        using var context = await SeedAsync();
        var position = Offset(metresNorth, 0);
        await AddStationAsync(context, 10, "Halt", position.Lat, position.Lon);

        var candidates = await NewMatcher(context).FindTripsForStationAsync(1, 10);

        Assert.Equal(expectedToMatch, candidates.Count > 0);
        if (expectedToMatch)
        {
            // A degree of longitude is only ~68 km at this latitude against ~111 km of latitude, so
            // anything treating the two alike would be out by a third here.
            Assert.InRange(candidates[0].DistanceMetres, metresNorth - 2, metresNorth + 2);
        }
    }

    [Fact]
    public async Task ProximityIsMeasuredEastWestToo()
    {
        // The same 280 m, but along the axis where degrees and metres diverge most.
        using var context = await SeedAsync(new LineString([
            new Coordinate(BaseLon, BaseLat - 0.1),
            new Coordinate(BaseLon, BaseLat + 0.1)
        ]));
        var position = Offset(0, 280);
        await AddStationAsync(context, 10, "Halt", position.Lat, position.Lon);

        var candidates = await NewMatcher(context).FindTripsForStationAsync(1, 10);

        var candidate = Assert.Single(candidates);
        Assert.InRange(candidate.DistanceMetres, 278, 282);
    }

    [Fact]
    public async Task StartingOrEndingHereIsStrongerThanPassingThrough()
    {
        var line = EastWestLineThroughBase();
        using var context = await SeedAsync(line);
        var terminus = new Coordinate(line.Coordinates[0].X, line.Coordinates[0].Y);
        await AddStationAsync(context, 10, "Terminus", terminus.Y, terminus.X);
        await AddStationAsync(context, 11, "Middle", BaseLat, BaseLon);

        var matcher = NewMatcher(context);

        Assert.Equal(VisitEvidence.RouteEndpoint, (await matcher.FindTripsForStationAsync(1, 10))[0].Evidence);
        Assert.Equal(VisitEvidence.Proximity, (await matcher.FindTripsForStationAsync(1, 11))[0].Evidence);
    }

    [Fact]
    public async Task AnEndpointNameCountsEvenWhenTheGeometryStopsShort()
    {
        // Routes are often clipped at the platform end, so the name is evidence the geometry lost.
        using var context = await SeedAsync(from: "Middle");
        await AddStationAsync(context, 10, "middle", BaseLat, BaseLon);

        var candidates = await NewMatcher(context).FindTripsForStationAsync(1, 10);

        Assert.Equal(VisitEvidence.RouteEndpoint, Assert.Single(candidates).Evidence);
    }

    [Fact]
    public async Task TripsComeBackOldestFirst()
    {
        using var context = await SeedAsync(dates:
        [
            new DateTime(2023, 1, 1),
            new DateTime(2016, 7, 9),
            new DateTime(2019, 4, 2)
        ]);
        await AddStationAsync(context, 10, "Halt", BaseLat, BaseLon);

        var candidates = await NewMatcher(context).FindTripsForStationAsync(1, 10);

        // The backfill preselects the first of these, and "earliest" is the whole question.
        Assert.Equal(
            [new DateTime(2016, 7, 9), new DateTime(2019, 4, 2), new DateTime(2023, 1, 1)],
            candidates.Select(c => c.Date));
    }

    [Fact]
    public async Task AnotherUsersTripsAreNeverProposed()
    {
        using var context = await SeedAsync(ownerUserId: 99);
        await AddStationAsync(context, 10, "Halt", BaseLat, BaseLon);

        Assert.Empty(await NewMatcher(context).FindTripsForStationAsync(1, 10));
    }

    [Fact]
    public async Task ARouteIsProposedOnceHoweverOftenItPassesTheStation()
    {
        // A line that doubles back past the same station: many near segments, one trip.
        var west = Offset(0, -5000);
        var east = Offset(0, 5000);
        using var context = await SeedAsync(new LineString([
            new Coordinate(west.Lon, west.Lat),
            new Coordinate(east.Lon, east.Lat),
            new Coordinate(west.Lon, west.Lat)
        ]));
        await AddStationAsync(context, 10, "Halt", BaseLat, BaseLon);

        var candidates = await NewMatcher(context).FindTripsForStationAsync(1, 10);

        Assert.Single(candidates);
    }

    [Fact]
    public async Task TripToStationsFindsWhatTheLinePassesStrongestFirst()
    {
        var line = EastWestLineThroughBase();
        using var context = await SeedAsync(line);
        var terminus = line.Coordinates[^1];
        await AddStationAsync(context, 10, "Middle", BaseLat, BaseLon);
        await AddStationAsync(context, 11, "Terminus", terminus.Y, terminus.X);
        var faraway = Offset(5000, 0);
        await AddStationAsync(context, 12, "Faraway", faraway.Lat, faraway.Lon);

        var candidates = await NewMatcher(context).FindStationsForTripAsync(1, 1);

        Assert.Equal([11, 10], candidates.Select(c => c.StationId));
        Assert.Equal(VisitEvidence.RouteEndpoint, candidates[0].Evidence);
        Assert.Equal(VisitEvidence.Proximity, candidates[1].Evidence);
    }

    [Fact]
    public async Task HiddenStationsAreNotProposed()
    {
        using var context = await SeedAsync();
        await AddStationAsync(context, 10, "Ghost", BaseLat, BaseLon, hidden: true);

        Assert.Empty(await NewMatcher(context).FindStationsForTripAsync(1, 1));
    }

    [Fact]
    public async Task AnotherUsersTripCannotBeInspected()
    {
        using var context = await SeedAsync(ownerUserId: 99);
        await AddStationAsync(context, 10, "Halt", BaseLat, BaseLon);

        Assert.Empty(await NewMatcher(context).FindStationsForTripAsync(1, 1));
    }

    [Fact]
    public async Task AStopBecomesTheStationItSitsOn()
    {
        using var context = await SeedAsync();
        await AddStationAsync(context, 10, "Halt", BaseLat, BaseLon);
        var nearby = Offset(60, 0);

        var matched = await NewMatcher(context).MatchStopsAsync([new StopPoint("Halt", nearby.Lat, nearby.Lon)]);

        var candidate = Assert.Single(matched);
        Assert.Equal(10, candidate.StationId);
        // A stop is the one thing that says the train stopped, rather than merely passed.
        Assert.Equal(VisitEvidence.Stopover, candidate.Evidence);
    }

    [Fact]
    public async Task AStopFarFromAnyStationMatchesNothing()
    {
        using var context = await SeedAsync();
        await AddStationAsync(context, 10, "Halt", BaseLat, BaseLon);
        var faraway = Offset(3000, 0);

        Assert.Empty(await NewMatcher(context).MatchStopsAsync([new StopPoint("Halt", faraway.Lat, faraway.Lon)]));
    }

    [Fact]
    public async Task AnAgreeingNameReachesFurtherThanPositionAlone()
    {
        // Big interchanges are a kilometre end to end and the two sources rarely pick the same
        // point on them, so the name is what rescues the match.
        using var context = await SeedAsync();
        await AddStationAsync(context, 10, "Grote Overstap", BaseLat, BaseLon);
        var offPlatform = Offset(600, 0);

        var matched = await NewMatcher(context).MatchStopsAsync([new StopPoint("grote overstap", offPlatform.Lat, offPlatform.Lon)]);

        Assert.Equal(10, Assert.Single(matched).StationId);
    }

    [Fact]
    public async Task ADisagreeingNameDoesNotReachFurther()
    {
        using var context = await SeedAsync();
        await AddStationAsync(context, 10, "Grote Overstap", BaseLat, BaseLon);
        var offPlatform = Offset(600, 0);

        // Without the name agreeing, 600 m is too far to call it the same station.
        Assert.Empty(await NewMatcher(context).MatchStopsAsync([new StopPoint("Somewhere else", offPlatform.Lat, offPlatform.Lon)]));
    }

    [Fact]
    public async Task RepeatedStopsAtOneStationCollapse()
    {
        using var context = await SeedAsync();
        await AddStationAsync(context, 10, "Halt", BaseLat, BaseLon);
        var a = Offset(30, 0);
        var b = Offset(60, 0);

        var matched = await NewMatcher(context).MatchStopsAsync(
            [new StopPoint("Halt", a.Lat, a.Lon), new StopPoint("Halt", b.Lat, b.Lon)]);

        var candidate = Assert.Single(matched);
        // And it keeps the closest reading of the two.
        Assert.InRange(candidate.DistanceMetres, 28, 32);
    }

    [Fact]
    public async Task AmongNamesakesTheNearestWins()
    {
        // Measured: 205 active stations (0.78%) have a same-named station within a kilometre —
        // border stations served by two operators, mostly. The widened name radius must not let a
        // namesake outrank the one the train actually called at.
        using var context = await SeedAsync();
        await AddStationAsync(context, 10, "Hendaye", BaseLat, BaseLon);
        var acrossTheBorder = Offset(400, 0);
        await AddStationAsync(context, 11, "Hendaye", acrossTheBorder.Lat, acrossTheBorder.Lon);
        var stop = Offset(350, 0);

        var matched = await NewMatcher(context).MatchStopsAsync([new StopPoint("Hendaye", stop.Lat, stop.Lon)]);

        Assert.Equal(11, Assert.Single(matched).StationId);
    }

    [Fact]
    public async Task StopMatchingNeverMarksAnythingVisited()
    {
        using var context = await SeedAsync();
        await AddStationAsync(context, 10, "Halt", BaseLat, BaseLon);

        await NewMatcher(context).MatchStopsAsync([new StopPoint("Halt", BaseLat, BaseLon)]);

        Assert.Empty(context.StationVisits);
    }

    [Fact]
    public async Task MatchingNeverMarksAnythingVisited()
    {
        // The base requirement, asserted rather than assumed: inference proposes, the user decides.
        using var context = await SeedAsync();
        await AddStationAsync(context, 10, "Halt", BaseLat, BaseLon);
        var matcher = NewMatcher(context);

        await matcher.FindTripsForStationAsync(1, 10);
        await matcher.FindStationsForTripAsync(1, 1);

        Assert.Empty(context.StationVisits);
    }

    [Fact]
    public async Task AnUnknownStationOrTripYieldsNothingRatherThanThrowing()
    {
        using var context = await SeedAsync();
        var matcher = NewMatcher(context);

        Assert.Empty(await matcher.FindTripsForStationAsync(1, 404));
        Assert.Empty(await matcher.FindStationsForTripAsync(1, 404));
    }

    [Fact]
    public async Task TheIndexIsBuiltOnceAndSharedAcrossCalls()
    {
        using var context = await SeedAsync();
        await AddStationAsync(context, 10, "Halt", BaseLat, BaseLon);
        var cache = new CountingCache();
        var matcher = new StationTripMatcher(context, cache);

        await matcher.FindTripsForStationAsync(1, 10);
        await matcher.FindStationsForTripAsync(1, 1);
        await matcher.FindTripsForStationAsync(1, 10);

        Assert.Equal(1, cache.Builds);
    }

    [Fact]
    public async Task InvalidatingForcesTheIndexToBeRebuilt()
    {
        using var context = await SeedAsync();
        await AddStationAsync(context, 10, "Halt", BaseLat, BaseLon);
        var cache = new CountingCache();
        var matcher = new StationTripMatcher(context, cache);

        await matcher.FindTripsForStationAsync(1, 10);
        cache.Invalidate();
        await matcher.FindTripsForStationAsync(1, 10);

        Assert.Equal(2, cache.Builds);
    }

    private sealed class CountingCache : IMatcherIndexCache
    {
        private readonly MatcherIndexCache _inner = new();
        public int Builds { get; private set; }

        public Task<MatcherIndex> GetAsync(Func<CancellationToken, Task<MatcherIndex>> build, CancellationToken cancellationToken = default) =>
            _inner.GetAsync(ct =>
            {
                Builds++;
                return build(ct);
            }, cancellationToken);

        public void Invalidate() => _inner.Invalidate();
    }
}
