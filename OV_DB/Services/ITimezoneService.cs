using System;
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
    }
}