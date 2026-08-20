using System;
using System.Collections.Generic;
using System.Linq;
using OV_DB.Models;
using OV_DB.Services;

namespace OV_DB.Tests;

// The backfill pre-selects by reading down this ordering — the earliest trip that starts or ends
// at the station, else simply the earliest — so the ordering is the rule worth pinning rather than
// the screen that reads it.
public class TripCandidateGroupingTests
{
    private static TripCandidate Candidate(
        int instanceId, int routeId, DateTime date, DateTime? start, VisitEvidence evidence = VisitEvidence.Proximity) =>
        new(instanceId, routeId, $"Route {routeId}", "A", "B", date, evidence, 50, start,
            start?.AddHours(1), "Train", "Trein", "#1E88E5", true);

    private static readonly DateTime Day = new(2021, 6, 1);

    [Fact]
    public void EarliestMeansEarliestByTimeNotJustByDate()
    {
        var groups = TripCandidateGrouping.Group([
            Candidate(1, 100, Day, Day.AddHours(18)),
            Candidate(2, 200, Day, Day.AddHours(6))
        ]);

        // Both ridden the same day and both of one grade, so the one boarded first leads.
        Assert.Equal([200, 100], groups.Select(g => g.RouteId));
    }

    [Fact]
    public void AGroupWithNoTimesLeadsThatDay()
    {
        var groups = TripCandidateGrouping.Group([
            Candidate(1, 100, Day, Day.AddHours(9)),
            Candidate(2, 200, Day, null)
        ]);

        // An unknown time could be any hour, so it cannot be shown to be later than 09:00.
        Assert.Equal([200, 100], groups.Select(g => g.RouteId));
    }

    [Fact]
    public void OrderIsChronologicalEvenWhenALaterTripTerminatesHere()
    {
        var groups = TripCandidateGrouping.Group([
            Candidate(1, 100, Day, Day.AddHours(6)),
            Candidate(2, 200, Day.AddYears(1), Day.AddYears(1), VisitEvidence.RouteEndpoint)
        ]);

        // The list answers "which trip brought me here first", so hoisting a terminating service a
        // year later above an earlier passing one would answer a different question. Evidence
        // decides which button leads, never the order.
        Assert.Equal([100, 200], groups.Select(g => g.RouteId));
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
