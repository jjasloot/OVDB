using System;
using System.Collections.Generic;
using System.Linq;
using OV_DB.Models;
using OV_DB.Services;

namespace OV_DB.Tests;

// What the backfill pre-selects comes straight out of this ordering, so the ordering is the rule
// worth pinning rather than the screen that reads it.
public class TripCandidateGroupingTests
{
    private static TripCandidate Candidate(
        int instanceId, int routeId, DateTime date, DateTime? start, VisitEvidence evidence = VisitEvidence.Proximity) =>
        new(instanceId, routeId, $"Route {routeId}", "A", "B", date, evidence, 50, start,
            start?.AddHours(1), "Train", "Trein", "#1E88E5");

    private static readonly DateTime Day = new(2021, 6, 1);

    [Fact]
    public void EarliestMeansEarliestByTimeNotJustByDate()
    {
        var groups = TripCandidateGrouping.Group([
            Candidate(1, 100, Day, Day.AddHours(18)),
            Candidate(2, 200, Day, Day.AddHours(6))
        ]);

        // Both ridden the same day, so the one boarded first leads and gets pre-selected.
        Assert.Equal([200, 100], groups.Select(g => g.RouteId));
    }

    [Fact]
    public void AGroupWithNoTimesDoesNotOutrankATimedOne()
    {
        var groups = TripCandidateGrouping.Group([
            Candidate(1, 100, Day, null),
            Candidate(2, 200, Day, Day.AddHours(9))
        ]);

        Assert.Equal([200, 100], groups.Select(g => g.RouteId));
    }

    [Fact]
    public void EndpointEvidenceStillWinsOverAnEarlierTime()
    {
        var groups = TripCandidateGrouping.Group([
            Candidate(1, 100, Day, Day.AddHours(6)),
            Candidate(2, 200, Day, Day.AddHours(18), VisitEvidence.RouteEndpoint)
        ]);

        // Starting or ending here is the only evidence of standing on the platform; a train that
        // merely rolled past earlier in the day is not a better answer for having been there.
        Assert.Equal([200, 100], groups.Select(g => g.RouteId));
    }

    [Fact]
    public void WithinARouteTheEarliestInstanceLeads()
    {
        var groups = TripCandidateGrouping.Group([
            Candidate(1, 100, Day, Day.AddHours(18)),
            Candidate(2, 100, Day, Day.AddHours(6)),
            Candidate(3, 100, Day.AddYears(1), Day.AddYears(1))
        ]);

        var group = Assert.Single(groups);
        Assert.Equal([2, 1, 3], group.Instances.Select(i => i.RouteInstanceId));
    }
}
