using System;
using System.Collections.Generic;

namespace OV_DB.Models
{
    /// <summary>
    /// What a visit means. Not stored: the level is derived from which dates are set on the row, so
    /// this exists only to say what the caller is asserting.
    /// </summary>
    public enum StationVisitLevel
    {
        /// <summary>A train the user was aboard stopped here. The weaker, default claim.</summary>
        Stopped = 0,
        /// <summary>The user got on or off here. Implies <see cref="Stopped"/>.</summary>
        EntryExit = 1
    }

    public class StationVisitUpdate
    {
        public bool Visited { get; set; }
        public StationVisitLevel Level { get; set; }
        /// <summary>
        /// Local date of the visit. Null leaves the visit undated, which is valid: it simply joins
        /// the backfill queue. The web map sends null, because marking from the sofa says nothing
        /// about when.
        /// </summary>
        public DateTime? Date { get; set; }
    }

    /// <summary>
    /// Both dates as the user says they are, rather than as a floor to be improved on. A trip id
    /// wins over the date beside it: the server takes the date from the trip, so the pair cannot
    /// drift apart.
    /// </summary>
    public class StationVisitDates
    {
        public DateTime? FirstStoppedDate { get; set; }
        public int? FirstStoppedRouteInstanceId { get; set; }
        public DateTime? FirstEntryExitDate { get; set; }
        public int? FirstEntryExitRouteInstanceId { get; set; }
    }

    public class StationVisitStateDTO
    {
        public bool Visited { get; set; }
        public StationVisitLevel? Level { get; set; }
        public DateTime? FirstStoppedDate { get; set; }
        public int? FirstStoppedRouteInstanceId { get; set; }
        public DateTime? FirstEntryExitDate { get; set; }
        public int? FirstEntryExitRouteInstanceId { get; set; }
        public double PercentageVisited { get; set; }
    }

    /// <summary>
    /// Candidate trips for one station, collapsed to one entry per route. Measured on real data,
    /// a station averages 60.8 candidate trips across 26.2 routes — a raw list nobody would read —
    /// and per route only the earliest instance can answer "when did I first come here". The rest
    /// stay available behind the row rather than in front of it.
    /// </summary>
    public class TripCandidateGroupDTO
    {
        public int RouteId { get; set; }
        public string RouteName { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        /// <summary>True when the route starts or ends here, which is the only evidence you were on the platform.</summary>
        public bool IsEndpoint { get; set; }
        public double DistanceMetres { get; set; }
        /// <summary>
        /// The kind of train, shown because the candidate list is otherwise a wall of dates. Only
        /// train-typed routes reach here at all: stations exist for railway stations, so a bus or
        /// tram trip cannot explain being at one however close its line runs.
        /// </summary>
        public string RouteTypeName { get; set; }
        public string RouteTypeNameNL { get; set; }
        public string RouteTypeColour { get; set; }
        public List<TripCandidateDTO> Instances { get; set; }
    }

    public class TripCandidateDTO
    {
        public int RouteInstanceId { get; set; }
        public DateTime Date { get; set; }
        /// <summary>Departure and arrival, where known — what tells two trips on one date apart.</summary>
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }
}
