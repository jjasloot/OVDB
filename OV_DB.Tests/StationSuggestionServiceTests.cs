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
/// nothing else, and that they never restate something the user has already answered.
/// </summary>
public class StationSuggestionServiceTests
{
    private const int TripId = 987;

    private static string StatusPayload(int tripId) => $$"""
    {
      "id": 123456,
      "checkin": {
        "trip": {{tripId}},
        "origin": { "station": { "id": 4711, "name": "Karlsruhe Hbf", "latitude": 48.993207, "longitude": 8.400977 } },
        "destination": { "station": { "id": 4712, "name": "Stuttgart Hbf", "latitude": 48.784084, "longitude": 9.181635 } }
      }
    }
    """;

    private static OVDBDatabaseContext NewContext()
    {
        var options = new DbContextOptionsBuilder<OVDBDatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OVDBDatabaseContext(options);
    }

    private static TrawellingStopover Stopover(string name, double lattitude, double longitude) =>
        new()
        {
            Uuid = Guid.NewGuid().ToString(),
            Station = new TrawellingStation { Name = name, Latitude = lattitude, Longitude = longitude }
        };

    /// <summary>The matcher stands in here: what it does with coordinates has its own tests.</summary>
    private static Mock<IStationTripMatcher> MatcherReturning(params StationCandidate[] candidates)
    {
        var matcher = new Mock<IStationTripMatcher>();
        matcher.Setup(m => m.MatchStopsAsync(It.IsAny<IEnumerable<StopPoint>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);
        return matcher;
    }

    private static Station StationRow(int id, string name) =>
        new() { Id = id, Name = name, Lattitude = 48.9, Longitude = 8.4 };

    private static User TestUser() => new() { Id = 7 };

    [Fact]
    public async Task SuggestsTheStationsTheOperatorSaysTheTrainCalledAt()
    {
        var context = NewContext();
        context.Stations.AddRange(StationRow(1, "Karlsruhe Hbf"), StationRow(2, "Pforzheim Hbf"));
        await context.SaveChangesAsync();

        var trawelling = new Mock<ITrawellingService>();
        trawelling.Setup(t => t.GetTripStopoversAsync(It.IsAny<User>(), TripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Stopover("Karlsruhe Hbf", 48.993207, 8.400977), Stopover("Pforzheim Hbf", 48.893, 8.708)]);
        var matcher = MatcherReturning(
            new StationCandidate(1, "Karlsruhe Hbf", VisitEvidence.Stopover, 12),
            new StationCandidate(2, "Pforzheim Hbf", VisitEvidence.Stopover, 30));

        var service = new StationSuggestionService(context, matcher.Object, trawelling.Object);
        var suggestions = await service.FromTrawellingStatusAsync(TestUser(), StatusPayload(TripId));

        Assert.Equal([1, 2], suggestions.Select(s => s.StationId));
        // The trip id came out of the payload the import already had, so this costs one call.
        trawelling.Verify(t => t.GetTripStopoversAsync(It.IsAny<User>(), TripId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnlyTheStopsWithCoordinatesReachTheMatcher()
    {
        var context = NewContext();
        context.Stations.Add(StationRow(1, "Karlsruhe Hbf"));
        await context.SaveChangesAsync();

        var noPosition = Stopover("Somewhere", 0, 0);
        noPosition.Station.Latitude = null;
        noPosition.Station.Longitude = null;
        var trawelling = new Mock<ITrawellingService>();
        trawelling.Setup(t => t.GetTripStopoversAsync(It.IsAny<User>(), TripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Stopover("Karlsruhe Hbf", 48.993207, 8.400977), noPosition]);

        List<StopPoint> seen = null;
        var matcher = new Mock<IStationTripMatcher>();
        matcher.Setup(m => m.MatchStopsAsync(It.IsAny<IEnumerable<StopPoint>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<StopPoint>, CancellationToken>((stops, _) => seen = stops.ToList())
            .ReturnsAsync([new StationCandidate(1, "Karlsruhe Hbf", VisitEvidence.Stopover, 12)]);

        var service = new StationSuggestionService(context, matcher.Object, trawelling.Object);
        await service.FromTrawellingStatusAsync(TestUser(), StatusPayload(TripId));

        Assert.Equal(["Karlsruhe Hbf"], seen.Select(s => s.Name));
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
            MatcherReturning(
                new StationCandidate(1, "Karlsruhe Hbf", VisitEvidence.Stopover, 12),
                new StationCandidate(2, "Pforzheim Hbf", VisitEvidence.Stopover, 30)).Object,
            new Mock<ITrawellingService>().Object);

        var suggestions = await service.FromStopsAsync(7, [new StopPoint("Karlsruhe Hbf", 48.99, 8.40)]);

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
            MatcherReturning(
                new StationCandidate(1, "Karlsruhe Hbf", VisitEvidence.Stopover, 12),
                new StationCandidate(2, "Pforzheim Hbf", VisitEvidence.Stopover, 30)).Object,
            new Mock<ITrawellingService>().Object);

        var suggestions = await service.FromStopsAsync(7, [new StopPoint("Karlsruhe Hbf", 48.99, 8.40)]);

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
            MatcherReturning(
                new StationCandidate(1, "Depot", VisitEvidence.Stopover, 5),
                new StationCandidate(2, "Marker", VisitEvidence.Stopover, 5),
                new StationCandidate(3, "Pforzheim Hbf", VisitEvidence.Stopover, 30)).Object,
            new Mock<ITrawellingService>().Object);

        var suggestions = await service.FromStopsAsync(7, [new StopPoint("Pforzheim Hbf", 48.89, 8.70)]);

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
        var service = new StationSuggestionService(NewContext(), MatcherReturning().Object, trawelling.Object);

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

        var service = new StationSuggestionService(NewContext(), MatcherReturning().Object, trawelling.Object);

        Assert.Empty(await service.FromTrawellingStatusAsync(TestUser(), StatusPayload(TripId)));
    }
}
