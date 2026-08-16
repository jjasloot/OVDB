using System;
using System.Collections.Generic;

namespace OV_DB.Models
{
    /// <summary>
    /// Aggregated punctuality figures for the trips that have both a scheduled and an actual
    /// time. Trips without scheduled times (i.e. anything not imported with planned data) are
    /// excluded from the delay figures but still counted in TotalTrips, so the UI can show how
    /// much of the history the numbers are based on.
    /// </summary>
    public class PunctualityStatsDTO
    {
        public int TotalTrips { get; set; }
        public int TripsWithDepartureData { get; set; }
        public int TripsWithArrivalData { get; set; }

        public double? AverageDepartureDelayMinutes { get; set; }
        public double? AverageArrivalDelayMinutes { get; set; }
        public double? MedianArrivalDelayMinutes { get; set; }

        /// <summary>A trip counts as punctual when it arrives less than this many minutes late.</summary>
        public int OnTimeThresholdMinutes { get; set; }
        public double? OnTimePercentage { get; set; }

        public List<DelayBucketDTO> ArrivalDelayDistribution { get; set; } = [];
        public List<GroupPunctualityDTO> ByOperator { get; set; } = [];
        public List<GroupPunctualityDTO> ByYear { get; set; } = [];
        public List<DelayedTripDTO> WorstTrips { get; set; } = [];
    }

    /// <summary>
    /// One column of the delay histogram. Key is a stable identifier the frontend translates,
    /// so bucket labels do not have to be localised server-side.
    /// </summary>
    public class DelayBucketDTO
    {
        public string Key { get; set; }
        public int Count { get; set; }
    }

    public class GroupPunctualityDTO
    {
        public string Label { get; set; }
        public int Trips { get; set; }
        public double AverageArrivalDelayMinutes { get; set; }
        public double OnTimePercentage { get; set; }
    }

    public class DelayedTripDTO
    {
        public int RouteInstanceId { get; set; }
        public int RouteId { get; set; }
        public DateTime Date { get; set; }
        public string Name { get; set; }
        public string NameNL { get; set; }
        public string Operator { get; set; }
        public double DelayMinutes { get; set; }
    }
}
