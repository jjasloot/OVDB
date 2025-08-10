using System;
using NetTopologySuite.Geometries;

namespace OV_DB.Services
{
    public interface ITimezoneService
    {
        /// <summary>
        /// Gets the timezone identifier for a given coordinate
        /// </summary>
        /// <param name="latitude">The latitude</param>
        /// <param name="longitude">The longitude</param>
        /// <returns>The timezone identifier (e.g., "Europe/Amsterdam")</returns>
        string GetTimezoneId(double latitude, double longitude);

        /// <summary>
        /// Gets timezone identifiers for the start and end points of a LineString
        /// </summary>
        /// <param name="lineString">The route LineString</param>
        /// <returns>A tuple of (startTimeZone, endTimeZone)</returns>
        (string startTimeZone, string endTimeZone) GetTimezones(LineString lineString);

        /// <summary>
        /// Converts a DateTime from one timezone to another
        /// </summary>
        /// <param name="dateTime">The source DateTime</param>
        /// <param name="fromTimeZone">Source timezone ID</param>
        /// <param name="toTimeZone">Target timezone ID</param>
        /// <returns>Converted DateTime</returns>
        DateTime ConvertTimezone(DateTime dateTime, string fromTimeZone, string toTimeZone);

        /// <summary>
        /// Calculates the duration between start and end times, handling timezone differences
        /// </summary>
        /// <param name="startTime">Start time</param>
        /// <param name="endTime">End time</param>
        /// <param name="startTimeZone">Start timezone ID</param>
        /// <param name="endTimeZone">End timezone ID</param>
        /// <returns>Duration in hours</returns>
        double CalculateDurationInHours(DateTime startTime, DateTime endTime, string startTimeZone, string endTimeZone);
    }
}