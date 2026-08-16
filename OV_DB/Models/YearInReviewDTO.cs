using System;
using System.Collections.Generic;

namespace OV_DB.Models
{
    /// <summary>
    /// A single year summarised for the "year in review" page. Everything here is derived from
    /// trips on the selected map; station visits are deliberately absent because StationVisit
    /// carries no timestamp, so "stations visited this year" cannot be determined.
    /// </summary>
    public class YearInReviewDTO
    {
        public int Year { get; set; }
        public int Trips { get; set; }
        public double DistanceKm { get; set; }
        public double DurationHours { get; set; }
        public int ActiveDays { get; set; }
        public int DistinctRoutes { get; set; }
        /// <summary>Routes ridden for the first time ever in this year.</summary>
        public int NewRoutes { get; set; }

        public List<CountryVisitDTO> Countries { get; set; } = [];
        public List<NameCountDTO> TopRouteTypes { get; set; } = [];
        public List<NameCountDTO> TopOperators { get; set; } = [];
        /// <summary>Distance per month, always twelve entries starting at January.</summary>
        public List<double> MonthlyDistanceKm { get; set; } = [];

        public HighlightTripDTO LongestTrip { get; set; }
        public HighlightTripDTO FastestTrip { get; set; }
        public BusiestDayDTO BusiestDay { get; set; }

        public double? OnTimePercentage { get; set; }
        public double? AverageArrivalDelayMinutes { get; set; }
        public int TripsWithArrivalData { get; set; }

        public int PreviousYearTrips { get; set; }
        public double PreviousYearDistanceKm { get; set; }
    }

    public class CountryVisitDTO
    {
        public string IsoCode { get; set; }
        public string FlagEmoji { get; set; }
        public string Name { get; set; }
        public string NameNL { get; set; }
    }

    public class NameCountDTO
    {
        public string Name { get; set; }
        public string NameNL { get; set; }
        public int Trips { get; set; }
        public double DistanceKm { get; set; }
    }

    public class HighlightTripDTO
    {
        public int RouteId { get; set; }
        public DateTime Date { get; set; }
        public string Name { get; set; }
        public string NameNL { get; set; }
        public double DistanceKm { get; set; }
        public double? DurationHours { get; set; }
        public double? AverageSpeedKmh { get; set; }
    }

    public class BusiestDayDTO
    {
        public DateTime Date { get; set; }
        public int Trips { get; set; }
        public double DistanceKm { get; set; }
    }
}
