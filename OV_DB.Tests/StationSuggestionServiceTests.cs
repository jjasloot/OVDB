using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using OV_DB.Models;
using OV_DB.Services;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Tests;

/// <summary>
/// Import suggestions, from the payload an import already holds to the stations it offers. What is
/// being pinned here is that suggestions come from the calling pattern the operator published and
/// nothing else, that they cover only the journey the user actually made, and that they never restate
/// something the user has already answered.
/// </summary>
public class StationSuggestionServiceTests
{
    private const int TripId = 987;

    private static OVDBDatabaseContext NewContext()
    {
        var options = new DbContextOptionsBuilder<OVDBDatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OVDBDatabaseContext(options);
    }

    private static TrawellingStopover Stopover(int stationId, string name, double lattitude = 48.9, double longitude = 8.4) =>
        new()
        {
            Uuid = Guid.NewGuid().ToString(),
            Station = new TrawellingStation { Id = stationId, Name = name, Latitude = lattitude, Longitude = longitude }
        };

    /// <summary>
    /// Stands in for the matcher by name: every stop resolves to the candidate called the same thing.
    /// A blanket stub would answer the endpoint lookup — a second call carrying only the two ends —
    /// with the whole list, and so could not show which stations the ends of the journey landed on.
    /// </summary>
    private static Mock<IStationTripMatcher> MatcherFor(params StationCandidate[] candidates)
    {
        var byName = candidates.ToDictionary(c => c.StationName);
        var matcher = new Mock<IStationTripMatcher>();
        matcher.Setup(m => m.MatchStopsAsync(It.IsAny<IEnumerable<StopPoint>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<StopPoint> stops, CancellationToken _) => Task.FromResult<IReadOnlyList<StationCandidate>>(
                stops.Where(s => byName.ContainsKey(s.Name)).Select(s => byName[s.Name]).ToList()));
        return matcher;
    }

    private static StationCandidate Candidate(int id, string name, double distance = 20) =>
        new(id, name, VisitEvidence.Stopover, distance);

    private static Station StationRow(int id, string name) =>
        new() { Id = id, Name = name, Lattitude = 48.9, Longitude = 8.4 };

    private static StopPoint Stop(string name) => new(name, 48.9, 8.4);

    private static User TestUser() => new() { Id = 7 };

    /// <summary>A three-station trip, boarded at the first station and left at the last.</summary>
    private static string StatusPayload(int tripId = TripId, int originId = 1, int destinationId = 3) => $$"""
    {
      "id": 123456,
      "checkin": {
        "trip": {{tripId}},
        "origin": { "station": { "id": {{originId}}, "name": "Origin", "latitude": 48.9, "longitude": 8.4 } },
        "destination": { "station": { "id": {{destinationId}}, "name": "Destination", "latitude": 48.9, "longitude": 8.4 } }
      }
    }
    """;

    [Fact]
    public async Task SuggestsTheStationsTheOperatorSaysTheTrainCalledAt()
    {
        var context = NewContext();
        context.Stations.AddRange(StationRow(1, "Karlsruhe Hbf"), StationRow(2, "Pforzheim Hbf"));
        await context.SaveChangesAsync();

        var service = new StationSuggestionService(
            context,
            MatcherFor(Candidate(1, "Karlsruhe Hbf", 12), Candidate(2, "Pforzheim Hbf", 30)).Object,
            new Mock<ITrawellingService>().Object);

        var suggestions = await service.FromStopsAsync(7, [Stop("Karlsruhe Hbf"), Stop("Pforzheim Hbf")]);

        Assert.Equal([1, 2], suggestions.Select(s => s.StationId));
    }

    [Fact]
    public async Task ReadsTheTripOutOfThePayloadTheImportAlreadyHas()
    {
        var context = NewContext();
        context.Stations.Add(StationRow(1, "Origin"));
        await context.SaveChangesAsync();

        var trawelling = new Mock<ITrawellingService>();
        trawelling.Setup(t => t.GetTripStopoversAsync(It.IsAny<User>(), TripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Stopover(1, "Origin"), Stopover(3, "Destination")]);

        var service = new StationSuggestionService(
            context, MatcherFor(Candidate(1, "Origin")).Object, trawelling.Object);
        var suggestions = await service.FromTrawellingStatusAsync(TestUser(), StatusPayload());

        Assert.Equal([1], suggestions.Select(s => s.StationId));
        // One call, because the trip id was already in the stored payload.
        trawelling.Verify(t => t.GetTripStopoversAsync(It.IsAny<User>(), TripId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TheEndsOfTheJourneyAreFlaggedAndTheMiddleIsNot()
    {
        var context = NewContext();
        context.Stations.AddRange(StationRow(1, "A"), StationRow(2, "B"), StationRow(3, "C"));
        await context.SaveChangesAsync();

        var service = new StationSuggestionService(
            context,
            MatcherFor(Candidate(1, "A"), Candidate(2, "B"), Candidate(3, "C")).Object,
            new Mock<ITrawellingService>().Object);

        var suggestions = await service.FromStopsAsync(7, [Stop("A"), Stop("B"), Stop("C")]);

        Assert.Equal([true, false, true], suggestions.Select(s => s.IsEndpoint));
    }

    [Fact]
    public async Task AnEndAlreadyMarkedLeavesTheStationsBehindItUnflagged()
    {
        // The regression this guards: the ends of a journey are usually the stations the user has
        // long since marked, so they drop out of the list. Reading "endpoint" off the position in
        // what is left offered "boarded here" for whichever station happened to survive.
        var context = NewContext();
        context.Stations.AddRange(StationRow(1, "A"), StationRow(2, "B"), StationRow(3, "C"));
        context.StationVisits.AddRange(
            new StationVisit { StationId = 1, UserId = 7 },
            new StationVisit { StationId = 3, UserId = 7 });
        await context.SaveChangesAsync();

        var service = new StationSuggestionService(
            context,
            MatcherFor(Candidate(1, "A"), Candidate(2, "B"), Candidate(3, "C")).Object,
            new Mock<ITrawellingService>().Object);

        var suggestions = await service.FromStopsAsync(7, [Stop("A"), Stop("B"), Stop("C")]);

        var only = Assert.Single(suggestions);
        Assert.Equal(2, only.StationId);
        Assert.False(only.IsEndpoint);
    }

    [Fact]
    public async Task ASingleStopIsNotAnEndpoint()
    {
        var context = NewContext();
        context.Stations.Add(StationRow(1, "A"));
        await context.SaveChangesAsync();

        var service = new StationSuggestionService(
            context, MatcherFor(Candidate(1, "A")).Object, new Mock<ITrawellingService>().Object);

        var suggestions = await service.FromStopsAsync(7, [Stop("A")]);

        Assert.False(Assert.Single(suggestions).IsEndpoint);
    }

    [Fact]
    public async Task OnlyTheSectionRiddenIsSuggested()
    {
        var context = NewContext();
        context.Stations.AddRange(
            StationRow(1, "Before"), StationRow(2, "Origin"), StationRow(3, "Middle"),
            StationRow(4, "Destination"), StationRow(5, "Beyond"));
        await context.SaveChangesAsync();

        var trawelling = new Mock<ITrawellingService>();
        trawelling.Setup(t => t.GetTripStopoversAsync(It.IsAny<User>(), TripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Stopover(10, "Before"), Stopover(20, "Origin"), Stopover(30, "Middle"),
                Stopover(40, "Destination"), Stopover(50, "Beyond")]);

        var service = new StationSuggestionService(
            context,
            MatcherFor(
                Candidate(1, "Before"), Candidate(2, "Origin"), Candidate(3, "Middle"),
                Candidate(4, "Destination"), Candidate(5, "Beyond")).Object,
            trawelling.Object);

        var suggestions = await service.FromTrawellingStatusAsync(
            TestUser(), StatusPayload(originId: 20, destinationId: 40));

        // The train ran on past where the user got off, and had been running before they boarded.
        // Those stations were never reached.
        Assert.Equal([2, 3, 4], suggestions.Select(s => s.StationId));
        Assert.Equal([true, false, true], suggestions.Select(s => s.IsEndpoint));
    }

    [Fact]
    public async Task AStationCalledAtTwiceTakesTheArrivalAfterBoarding()
    {
        var context = NewContext();
        context.Stations.AddRange(StationRow(1, "Loop"), StationRow(2, "Origin"), StationRow(3, "Far"));
        await context.SaveChangesAsync();

        var trawelling = new Mock<ITrawellingService>();
        // The service calls at the destination before the user boards, and again afterwards.
        trawelling.Setup(t => t.GetTripStopoversAsync(It.IsAny<User>(), TripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Stopover(10, "Loop"), Stopover(20, "Origin"), Stopover(30, "Far"), Stopover(10, "Loop")]);

        var service = new StationSuggestionService(
            context,
            MatcherFor(Candidate(1, "Loop"), Candidate(2, "Origin"), Candidate(3, "Far")).Object,
            trawelling.Object);

        var suggestions = await service.FromTrawellingStatusAsync(
            TestUser(), StatusPayload(originId: 20, destinationId: 10));

        // Boarded at Origin and left at the second call at Loop, so Far is on the way and the first
        // call at Loop is not.
        Assert.Equal([2, 3, 1], suggestions.Select(s => s.StationId));
    }

    [Fact]
    public async Task TheWholePatternIsKeptWhenTheEndsAreNotInIt()
    {
        var context = NewContext();
        context.Stations.AddRange(StationRow(1, "A"), StationRow(2, "B"));
        await context.SaveChangesAsync();

        var trawelling = new Mock<ITrawellingService>();
        trawelling.Setup(t => t.GetTripStopoversAsync(It.IsAny<User>(), TripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Stopover(10, "A"), Stopover(20, "B")]);

        var service = new StationSuggestionService(
            context, MatcherFor(Candidate(1, "A"), Candidate(2, "B")).Object, trawelling.Object);

        // Neither the origin nor the destination appears among the stopovers.
        var suggestions = await service.FromTrawellingStatusAsync(
            TestUser(), StatusPayload(originId: 999, destinationId: 998));

        // Over-offering is recoverable - the user says no. Dropping stations they did visit is not.
        Assert.Equal([1, 2], suggestions.Select(s => s.StationId));
    }

    [Fact]
    public async Task OnlyTheStopsWithCoordinatesReachTheMatcher()
    {
        var context = NewContext();
        context.Stations.Add(StationRow(1, "Origin"));
        await context.SaveChangesAsync();

        var noPosition = Stopover(3, "Destination");
        noPosition.Station.Latitude = null;
        noPosition.Station.Longitude = null;
        var trawelling = new Mock<ITrawellingService>();
        trawelling.Setup(t => t.GetTripStopoversAsync(It.IsAny<User>(), TripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Stopover(1, "Origin"), noPosition]);

        var seen = new List<List<StopPoint>>();
        var matcher = MatcherFor(Candidate(1, "Origin"));
        matcher.Setup(m => m.MatchStopsAsync(It.IsAny<IEnumerable<StopPoint>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<StopPoint> stops, CancellationToken _) =>
            {
                var list = stops.ToList();
                seen.Add(list);
                return Task.FromResult<IReadOnlyList<StationCandidate>>(
                    list.Where(s => s.Name == "Origin").Select(s => Candidate(1, "Origin")).ToList());
            });

        var service = new StationSuggestionService(context, matcher.Object, trawelling.Object);
        await service.FromTrawellingStatusAsync(TestUser(), StatusPayload());

        Assert.Equal(["Origin"], seen[0].Select(s => s.Name));
    }

    [Fact]
    public async Task AStationAlreadyMarkedIsNotSuggestedAgain()
    {
        var context = NewContext();
        context.Stations.AddRange(StationRow(1, "Karlsruhe Hbf"), StationRow(2, "Pforzheim Hbf"));
        context.StationVisits.Add(new StationVisit { StationId = 1, UserId = 7 });
        await context.SaveChangesAsync();

        var service = new StationSuggestionService(
            context,
            MatcherFor(Candidate(1, "Karlsruhe Hbf"), Candidate(2, "Pforzheim Hbf")).Object,
            new Mock<ITrawellingService>().Object);

        var suggestions = await service.FromStopsAsync(7, [Stop("Karlsruhe Hbf"), Stop("Pforzheim Hbf")]);

        Assert.Equal([2], suggestions.Select(s => s.StationId));
    }

    [Fact]
    public async Task AStationTheUserWavedAwayIsNotSuggestedAgain()
    {
        var context = NewContext();
        context.Stations.AddRange(StationRow(1, "Karlsruhe Hbf"), StationRow(2, "Pforzheim Hbf"));
        context.StationSuggestionDismissals.Add(new StationSuggestionDismissal
        {
            StationId = 2,
            UserId = 7,
            DismissedOn = new DateTime(2026, 8, 1)
        });
        await context.SaveChangesAsync();

        var service = new StationSuggestionService(
            context,
            MatcherFor(Candidate(1, "Karlsruhe Hbf"), Candidate(2, "Pforzheim Hbf")).Object,
            new Mock<ITrawellingService>().Object);

        var suggestions = await service.FromStopsAsync(7, [Stop("Karlsruhe Hbf"), Stop("Pforzheim Hbf")]);

        Assert.Equal([1], suggestions.Select(s => s.StationId));
    }

    [Fact]
    public async Task HiddenAndSpecialStationsAreNeverSuggested()
    {
        var context = NewContext();
        var hidden = StationRow(1, "Depot");
        hidden.Hidden = true;
        var special = StationRow(2, "Marker");
        special.Special = true;
        context.Stations.AddRange(hidden, special, StationRow(3, "Pforzheim Hbf"));
        await context.SaveChangesAsync();

        var service = new StationSuggestionService(
            context,
            MatcherFor(Candidate(1, "Depot"), Candidate(2, "Marker"), Candidate(3, "Pforzheim Hbf")).Object,
            new Mock<ITrawellingService>().Object);

        var suggestions = await service.FromStopsAsync(
            7, [Stop("Depot"), Stop("Marker"), Stop("Pforzheim Hbf")]);

        Assert.Equal([3], suggestions.Select(s => s.StationId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not json")]
    [InlineData("{\"id\": 1}")]
    [InlineData("{\"id\": 1, \"checkin\": { \"trip\": 0 } }")]
    public async Task WithoutATripThereIsNothingToAsk(string payload)
    {
        // Check-ins with no trip behind them happen: manual entries, older payloads. Suggestions sit
        // on top of an import that has already succeeded, so this has to be quiet, not an error.
        var trawelling = new Mock<ITrawellingService>();
        var service = new StationSuggestionService(NewContext(), MatcherFor().Object, trawelling.Object);

        Assert.Empty(await service.FromTrawellingStatusAsync(TestUser(), payload));
        trawelling.Verify(
            t => t.GetTripStopoversAsync(It.IsAny<User>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ATripWithNoCallingPatternSuggestsNothing()
    {
        // No falling back to geometry here: a line passing a platform is not the operator saying the
        // train called there, and this is the only signal allowed to propose a station.
        var trawelling = new Mock<ITrawellingService>();
        trawelling.Setup(t => t.GetTripStopoversAsync(It.IsAny<User>(), TripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new StationSuggestionService(NewContext(), MatcherFor().Object, trawelling.Object);

        Assert.Empty(await service.FromTrawellingStatusAsync(TestUser(), StatusPayload()));
    }
}
