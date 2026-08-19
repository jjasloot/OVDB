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

        /// <summary>
        /// The pre-selected route's line, sent with the station rather than fetched after it. The
        /// default is what gets drawn nearly every time, so making it a second round trip only ever
        /// added a visible delay between the station appearing and its route showing up.
        /// </summary>
        public RouteGeometryDTO SuggestedRouteGeometry { get; set; }

        /// <summary>
        /// How far along the region this station sits in. Null where the station has no region below
        /// its country.
        /// </summary>
        public RegionProgressDTO RegionProgress { get; set; }
    }

    /// <summary>
    /// Dating progress for one region.
    /// </summary>
    /// <remarks>
    /// The queue is 4,970 stations long, so no single answer visibly moves it — a bar over the whole
    /// thing reads as stationary however much work goes in. A province does move, and can be finished
    /// in a sitting, which is the only kind of progress worth showing. The queue now sweeps
    /// geographically, so the region stays the same for a long run of stations and then changes, which
    /// gives the run an end.
    /// </remarks>
    public class RegionProgressDTO
    {
        public string Name { get; set; }
        /// <summary>Visits in this region that carry a date.</summary>
        public int Dated { get; set; }
        /// <summary>Visits in this region, dated or not.</summary>
        public int Total { get; set; }
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
