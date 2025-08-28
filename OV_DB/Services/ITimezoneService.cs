using System;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;

namespace OV_DB.Services
{
    public interface ITimezoneService
    {
        /// <summary>
        /// Calculates the duration between start and end times, handling timezone differences
        /// </summary>
        /// <param name="startTime">Start time</param>
        /// <param name="endTime">End time</param>
        /// <param name="lineString">Route geometry for timezone lookup</param>
        /// <returns>Duration in hours</returns>
        double CalculateDurationInHours(DateTime startTime, DateTime endTime, LineString lineString);

        /// <summary>
        /// Convert UTC datetime to local time based on coordinates
        /// </summary>
        /// <param name="utcDateTime">UTC datetime</param>
        /// <param name="latitude">Latitude coordinate</param>
        /// <param name="longitude">Longitude coordinate</param>
        /// <returns>Local datetime</returns>
        Task<DateTime> ConvertUtcToLocalTimeAsync(DateTime utcDateTime, double latitude, double longitude);
    }
}