using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace OVDB_database.Models
{
    [Index(nameof(Date))]
    [Index(nameof(Date), nameof(RouteId), Name = "idx_routeinstances_date_routeid")]
    [Index(nameof(RouteId), nameof(Date), Name = "idx_routeinstances_routeid_date")]
    [Index(nameof(TrawellingStatusId))]
    public class RouteInstance
    {
        [Key]
        public int RouteInstanceId { get; set; }
        public int RouteId { get; set; }
        public Route Route { get; set; }
        public DateTime Date { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        /// <summary>
        /// Scheduled (planned) departure time for this trip instance
        /// </summary>
        public DateTime? ScheduledStartTime { get; set; }
        /// <summary>
        /// Scheduled (planned) arrival time for this trip instance
        /// </summary>
        public DateTime? ScheduledEndTime { get; set; }
        /// <summary>
        /// Duration of the trip in hours (calculated and stored to avoid timezone computations)
        /// </summary>
        public double? DurationHours { get; set; }
        public List<RouteInstanceProperty> RouteInstanceProperties { get; set; }
        public List<RouteInstanceMap> RouteInstanceMaps { get; set; }
        
        /// <summary>
        /// Link to Träwelling status ID for imported trips
        /// </summary>
        public int? TrawellingStatusId { get; set; }

        /// <summary>
        /// Departure delay in minutes (positive = late, negative = early). Null if scheduled or actual departure time is missing.
        /// </summary>
        public double? DepartureDelayMinutes =>
            StartTime.HasValue && ScheduledStartTime.HasValue
                ? (StartTime.Value - ScheduledStartTime.Value).TotalMinutes
                : null;

        /// <summary>
        /// Arrival delay in minutes (positive = late, negative = early). Null if scheduled or actual arrival time is missing.
        /// </summary>
        public double? ArrivalDelayMinutes =>
            EndTime.HasValue && ScheduledEndTime.HasValue
                ? (EndTime.Value - ScheduledEndTime.Value).TotalMinutes
                : null;

        /// <summary>
        /// Calculates the average speed in km/h based on stored duration
        /// </summary>
        /// <returns>Average speed in km/h, or null if duration or distance are not available</returns>
        public double? GetAverageSpeedKmh()
        {
            if (DurationHours == null || DurationHours <= 0 || Route == null)
                return null;

            var distance = Route.OverrideDistance ?? Route.CalculatedDistance;
            if (distance <= 0)
                return null;

            return distance / DurationHours.Value;
        }
    }
}
