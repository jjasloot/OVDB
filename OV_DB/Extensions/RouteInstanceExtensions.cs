using OVDB_database.Models;
using OV_DB.Services;

namespace OV_DB.Extensions
{
    public static class RouteInstanceExtensions
    {
        /// <summary>
        /// Calculates the average speed for a route instance in km/h
        /// </summary>
        /// <param name="instance">The route instance</param>
        /// <param name="timezoneService">The timezone service for calculating duration</param>
        /// <returns>Average speed in km/h, or null if times or distance are not available</returns>
        public static double? CalculateAverageSpeed(this RouteInstance instance, ITimezoneService timezoneService)
        {
            if (instance.StartTime == null || instance.EndTime == null || instance.Route == null)
                return null;

            var distance = instance.Route.OverrideDistance ?? instance.Route.CalculatedDistance;
            if (distance <= 0)
                return null;

            try
            {
                var (startTimeZone, endTimeZone) = timezoneService.GetTimezones(instance.Route.LineString);
                var durationHours = timezoneService.CalculateDurationInHours(
                    instance.StartTime.Value, 
                    instance.EndTime.Value, 
                    startTimeZone, 
                    endTimeZone);

                if (durationHours <= 0)
                    return null;

                return distance / durationHours;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the duration of the trip in hours, accounting for timezones
        /// </summary>
        /// <param name="instance">The route instance</param>
        /// <param name="timezoneService">The timezone service</param>
        /// <returns>Duration in hours, or null if times are not available</returns>
        public static double? GetDurationInHours(this RouteInstance instance, ITimezoneService timezoneService)
        {
            if (instance.StartTime == null || instance.EndTime == null || instance.Route == null)
                return null;

            try
            {
                var (startTimeZone, endTimeZone) = timezoneService.GetTimezones(instance.Route.LineString);
                return timezoneService.CalculateDurationInHours(
                    instance.StartTime.Value,
                    instance.EndTime.Value,
                    startTimeZone,
                    endTimeZone);
            }
            catch
            {
                return null;
            }
        }
    }
}