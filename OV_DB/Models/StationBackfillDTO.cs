using System;
using System.Collections.Generic;

namespace OV_DB.Models
{
    /// <summary>
    /// One station's worth of the dating queue: where it is, what could have brought you there, and
    /// which of those is the least-bad default.
    /// </summary>
    public class StationBackfillItemDTO
    {
        /// <summary>How many undated visits are left, so the end is visible from the start.</summary>
        public int Remaining { get; set; }
        public int StationId { get; set; }
        public string StationName { get; set; }
        public double Lattitude { get; set; }
        public double Longitude { get; set; }
        public IEnumerable<string> Regions { get; set; }
        public List<TripCandidateGroupDTO> Candidates { get; set; }
        /// <summary>
        /// The trip to pre-select: the earliest candidate. The question is which trip brought you
        /// here first, so a later terminating service must not outrank an earlier passing one.
        /// Where the earliest only passes and a later one terminates here, the screen leads with
        /// "stopped then, got off later" rather than pre-selecting the later trip outright.
        /// </summary>
        public int? SuggestedRouteInstanceId { get; set; }
        /// <summary>True when the suggestion rests on the route starting or ending here.</summary>
        public bool SuggestionIsEndpoint { get; set; }
    }

    /// <summary>A route drawn on the backfill map, simplified for display.</summary>
    public class RouteGeometryDTO
    {
        public int RouteId { get; set; }
        /// <summary>[lattitude, longitude] pairs, which is the order Leaflet wants.</summary>
        public List<double[]> Coordinates { get; set; }
    }

    /// <summary>A station a trip explains but that is not marked visited.</summary>
    public class StationSuggestionDTO
    {
        public int StationId { get; set; }
        public string StationName { get; set; }
        public double Lattitude { get; set; }
        public double Longitude { get; set; }
        public bool IsEndpoint { get; set; }
        public double DistanceMetres { get; set; }
    }

    /// <summary>
    /// Suggestions for a just-imported route, with the trip that can date them. Null when the route
    /// has no trip yet — then anything ticked is recorded undated and joins the backfill queue.
    /// </summary>
    public class StationSuggestionsForRouteDTO
    {
        public int? RouteInstanceId { get; set; }
        public List<StationSuggestionDTO> Stations { get; set; }
    }

    public class MarkSuggestionDTO
    {
        public int StationId { get; set; }
        /// <summary>
        /// The trip that supplies the date. Null for an OSM import, which has no trip on it yet —
        /// the visit is then recorded undated rather than claiming a date nothing knows.
        /// </summary>
        public int? RouteInstanceId { get; set; }
        public StationVisitLevel Level { get; set; }
    }
}
