using System;
using GeoTimeZone;
using NetTopologySuite.Geometries;

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
                    // On Windows, try to convert IANA to Windows timezone ID
                    var windowsId = ConvertIanaToWindows(ianaTimeZoneId);
                    return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                }
                catch
                {
                    // Ultimate fallback to UTC
                    return TimeZoneInfo.Utc;
                }
            }
        }

        private string ConvertIanaToWindows(string ianaTimeZoneId)
        {
            // Use TimeZoneConverter library or implement basic conversion
            // For now, fallback to a few common ones and UTC for others
            return ianaTimeZoneId switch
            {
                "Europe/Amsterdam" => "W. Europe Standard Time",
                "Europe/London" => "GMT Standard Time", 
                "Europe/Paris" => "Romance Standard Time",
                "Europe/Berlin" => "W. Europe Standard Time",
                "Europe/Rome" => "W. Europe Standard Time",
                "America/New_York" => "Eastern Standard Time",
                "America/Chicago" => "Central Standard Time",
                "America/Denver" => "Mountain Standard Time",
                "America/Los_Angeles" => "Pacific Standard Time",
                "Asia/Tokyo" => "Tokyo Standard Time",
                "Asia/Shanghai" => "China Standard Time",
                "UTC" => "UTC",
                _ => "UTC" // Fallback to UTC for unknown timezones
            };
        }
    }
}