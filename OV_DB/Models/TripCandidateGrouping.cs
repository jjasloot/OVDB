using System;
using System.Collections.Generic;
using System.Linq;
using OV_DB.Services;

namespace OV_DB.Models
{
    /// <summary>
    /// Turns a flat candidate list into something a person can read.
    /// </summary>
    /// <remarks>
    /// Measured on real data: a station averages 60.8 candidate trips across 26.2 routes. Per route
    /// only the earliest instance can answer "when did I first come here", so that one leads and the
    /// rest sit behind it.
    /// <para>
    /// Ordering is strictly chronological — <b>not</b> endpoint-grade first. The question is which
    /// trip brought you here <em>first</em>, so a list that hoists a 2023 terminating service above a
    /// 2017 passing one is answering a different question. Evidence still decides which button leads,
    /// and a route that only passes earlier while a later one terminates here is exactly the
    /// "stopped then, got off later" case.
    /// </para>
    /// </remarks>
    public static class TripCandidateGrouping
    {
        public static List<TripCandidateGroupDTO> Group(IEnumerable<TripCandidate> candidates) =>
            candidates
                .GroupBy(c => c.RouteId)
                .Select(g => new TripCandidateGroupDTO
                {
                    RouteId = g.Key,
                    RouteName = g.First().RouteName,
                    From = g.First().From,
                    To = g.First().To,
                    IsEndpoint = g.Any(c => c.Evidence == VisitEvidence.RouteEndpoint),
                    DistanceMetres = g.Min(c => c.DistanceMetres),
                    RouteTypeName = g.First().RouteTypeName,
                    RouteTypeNameNL = g.First().RouteTypeNameNL,
                    RouteTypeColour = g.First().RouteTypeColour,
                    // A trip with no time sorts first among that day's trips: it could have been any
                    // hour, so it cannot be shown to be later than one that names a time.
                    Instances = g.OrderBy(c => c.Date).ThenBy(c => c.StartTime ?? DateTime.MinValue)
                        .Select(c => new TripCandidateDTO
                        {
                            RouteInstanceId = c.RouteInstanceId,
                            Date = c.Date,
                            StartTime = c.StartTime,
                            EndTime = c.EndTime
                        })
                        .ToList()
                })
                .OrderBy(g => g.Instances[0].Date)
                // Earliest actually means earliest, not just earliest date: where two routes were
                // both ridden on the same day, the one boarded first leads.
                .ThenBy(g => g.Instances[0].StartTime ?? DateTime.MinValue)
                .ToList();
    }
}
