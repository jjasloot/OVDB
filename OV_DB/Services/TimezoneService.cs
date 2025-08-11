using System;
using GeoTimeZone;
using NetTopologySuite.Geometries;
using TimeZoneConverter;

namespace OV_DB.Services
{
    public class TimezoneService : ITimezoneService
    {
        public double CalculateDurationInHours(DateTime startTime, DateTime endTime, LineString lineString)
        {
            if (lineString == null || lineString.Coordinates.Length == 0)
            {
                // Fallback to simple duration calculation
                return (endTime - startTime).TotalHours;
            }

            try
            {
                // Get timezone IDs for start and end points
                var startCoord = lineString.Coordinates[0];
                var endCoord = lineString.Coordinates[lineString.Coordinates.Length - 1];

                var startTimeZoneId = GetTimezoneId(startCoord.Y, startCoord.X);
                var endTimeZoneId = GetTimezoneId(endCoord.Y, endCoord.X);

                // Convert both times to UTC for accurate calculation
                var startTz = GetTimeZoneInfo(startTimeZoneId);
                var endTz = GetTimeZoneInfo(endTimeZoneId);

                var startUtc = TimeZoneInfo.ConvertTimeToUtc(startTime, startTz);
                var endUtc = TimeZoneInfo.ConvertTimeToUtc(endTime, endTz);

                var duration = endUtc - startUtc;
                return duration.TotalHours;
            }
            catch
            {
                // Fallback to simple duration calculation if timezone lookup fails
                return (endTime - startTime).TotalHours;
            }
        }

        private string GetTimezoneId(double latitude, double longitude)
        {
            try
            {
                return TimeZoneLookup.GetTimeZone(latitude, longitude).Result;
            }
            catch
            {
                // Fallback to UTC if lookup fails
                return "UTC";
            }
        }

        private TimeZoneInfo GetTimeZoneInfo(string ianaTimeZoneId)
        {
            try
            {
                // Try to find the timezone directly (works on Linux/macOS)
                return TimeZoneInfo.FindSystemTimeZoneById(ianaTimeZoneId);
            }
            catch
            {
                try
                {
                    // On Windows, use TimeZoneConverter library to convert IANA to Windows timezone ID
                    var windowsId = TZConvert.IanaToWindows(ianaTimeZoneId);
                    return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                }
                catch
                {
                    // Ultimate fallback to UTC
                    return TimeZoneInfo.Utc;
                }
            }
        }
    }
}