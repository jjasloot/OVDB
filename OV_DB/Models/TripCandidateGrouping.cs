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
    /// rest sit behind it. Endpoint routes come first because starting or ending somewhere is the
    /// only evidence you stood on the platform rather than rolled past it.
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
                    Instances = g.OrderBy(c => c.Date)
                        .Select(c => new TripCandidateDTO { RouteInstanceId = c.RouteInstanceId, Date = c.Date })
                        .ToList()
                })
                .OrderByDescending(g => g.IsEndpoint)
                .ThenBy(g => g.Instances[0].Date)
                .ToList();
    }
}
