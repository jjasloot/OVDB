using System;
using System.Collections.Generic;

namespace OV_DB.Models
{
    public class TripFrequencyDTO
    {
        public DateTime Date { get; set; }
        public int TripCount { get; set; }
        public double TotalDistance { get; set; }
    }

    public class ActivityHeatmapDTO
    {
        public int Year { get; set; }
        public List<ActivityDataPoint> Data { get; set; } = new List<ActivityDataPoint>();
    }

    public class ActivityDataPoint
    {
        public string Date { get; set; } = string.Empty;
        public int TripCount { get; set; }
        public double TotalDistance { get; set; }
        public int Level { get; set; } // 0-4 for heatmap intensity
    }

    public class RouteRankingDTO
    {
        public int RouteId { get; set; }
        public string? RouteName { get; set; }
        public string? RouteType { get; set; }
        public string? RouteTypeNameNL { get; set; }
        public int TripCount { get; set; }
        public double TotalDistance { get; set; }
        public double AverageDistance { get; set; }
        public DateTime FirstTrip { get; set; }
        public DateTime LastTrip { get; set; }
    }

    public class TravelTimeTrendsDTO
    {
        public List<MonthlyTrendDTO> AverageDurationByMonth { get; set; } = new List<MonthlyTrendDTO>();
        public List<HourlyTrendDTO> AverageDurationByHour { get; set; } = new List<HourlyTrendDTO>();
        public List<RouteTypeTrendDTO> AverageDurationByRouteType { get; set; } = new List<RouteTypeTrendDTO>();
    }

    public class MonthlyTrendDTO
    {
        public string Month { get; set; } = string.Empty;
        public double AverageDuration { get; set; }
        public int TripCount { get; set; }
    }

    public class HourlyTrendDTO
    {
        public int Hour { get; set; }
        public double AverageDuration { get; set; }
        public int TripCount { get; set; }
    }

    public class RouteTypeTrendDTO
    {
        public string? RouteType { get; set; }
        public string? RouteTypeNL { get; set; }
        public double AverageDuration { get; set; }
        public double AverageSpeed { get; set; }
        public int TripCount { get; set; }
    }

    public class IntegrationStatsDTO
    {
        public int TotalTrips { get; set; }
        public int TripsWithTiming { get; set; }
        public int TraewellingImported { get; set; }
        public int TripsWithSource { get; set; }
        public double TimingCompleteness { get; set; }
        public List<SourceBreakdownDTO> SourceBreakdown { get; set; } = new List<SourceBreakdownDTO>();
        public List<MonthlyImportDTO> MonthlyImports { get; set; } = new List<MonthlyImportDTO>();
    }

    public class SourceBreakdownDTO
    {
        public string? Source { get; set; }
        public int Count { get; set; }
    }

    public class MonthlyImportDTO
    {
        public string Month { get; set; } = string.Empty;
        public int ImportedCount { get; set; }
    }

    public class CoverageOverviewDTO
    {
        public int UniqueRoutes { get; set; }
        public double TotalDistance { get; set; }
        public int VisitedStations { get; set; }
        public List<RouteTypeBreakdownDTO> RouteTypeBreakdown { get; set; } = new List<RouteTypeBreakdownDTO>();
        public string CoverageNote { get; set; } = string.Empty;
    }

    public class RouteTypeBreakdownDTO
    {
        public string? RouteType { get; set; }
        public string? RouteTypeNL { get; set; }
        public int UniqueRoutes { get; set; }
        public int TripCount { get; set; }
        public double TotalDistance { get; set; }
    }
}