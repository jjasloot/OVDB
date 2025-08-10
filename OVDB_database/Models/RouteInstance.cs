using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace OVDB_database.Models
{
    [Index(nameof(Date))]
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
        /// Duration of the trip in hours (calculated and stored to avoid timezone computations)
        /// </summary>
        public double? DurationHours { get; set; }
        public List<RouteInstanceProperty> RouteInstanceProperties { get; set; }
        public List<RouteInstanceMap> RouteInstanceMaps { get; set; }

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
