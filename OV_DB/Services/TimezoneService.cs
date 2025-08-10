using System;
using GeoTimeZone;
using NetTopologySuite.Geometries;

namespace OV_DB.Services
{
    public class TimezoneService : ITimezoneService
    {
        public string GetTimezoneId(double latitude, double longitude)
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

        public (string startTimeZone, string endTimeZone) GetTimezones(LineString lineString)
        {
            if (lineString == null || lineString.Coordinates.Length == 0)
            {
                return ("UTC", "UTC");
            }

            var startCoord = lineString.Coordinates[0];
            var endCoord = lineString.Coordinates[lineString.Coordinates.Length - 1];

            var startTimeZone = GetTimezoneId(startCoord.Y, startCoord.X);
            var endTimeZone = GetTimezoneId(endCoord.Y, endCoord.X);

            return (startTimeZone, endTimeZone);
        }

        public DateTime ConvertTimezone(DateTime dateTime, string fromTimeZone, string toTimeZone)
        {
            try
            {
                var fromTz = TimeZoneInfo.FindSystemTimeZoneById(GetSystemTimeZoneId(fromTimeZone));
                var toTz = TimeZoneInfo.FindSystemTimeZoneById(GetSystemTimeZoneId(toTimeZone));

                var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(dateTime, fromTz);
                return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, toTz);
            }
            catch
            {
                // If conversion fails, return original dateTime
                return dateTime;
            }
        }

        public double CalculateDurationInHours(DateTime startTime, DateTime endTime, string startTimeZone, string endTimeZone)
        {
            try
            {
                // Convert both times to UTC for accurate calculation
                var startTz = TimeZoneInfo.FindSystemTimeZoneById(GetSystemTimeZoneId(startTimeZone));
                var endTz = TimeZoneInfo.FindSystemTimeZoneById(GetSystemTimeZoneId(endTimeZone));

                var startUtc = TimeZoneInfo.ConvertTimeToUtc(startTime, startTz);
                var endUtc = TimeZoneInfo.ConvertTimeToUtc(endTime, endTz);

                var duration = endUtc - startUtc;
                return duration.TotalHours;
            }
            catch
            {
                // Fallback to simple duration calculation
                return (endTime - startTime).TotalHours;
            }
        }

        private string GetSystemTimeZoneId(string ianaTimeZoneId)
        {
            // Map common IANA timezone IDs to Windows timezone IDs when running on Windows
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                return ianaTimeZoneId switch
                {
                    "Europe/Amsterdam" => "W. Europe Standard Time",
                    "Europe/London" => "GMT Standard Time",
                    "Europe/Paris" => "Romance Standard Time",
                    "Europe/Berlin" => "W. Europe Standard Time",
                    "Europe/Rome" => "W. Europe Standard Time",
                    "Europe/Madrid" => "Romance Standard Time",
                    "Europe/Prague" => "Central Europe Standard Time",
                    "Europe/Warsaw" => "Central European Standard Time",
                    "Europe/Vienna" => "W. Europe Standard Time",
                    "Europe/Zurich" => "W. Europe Standard Time",
                    "America/New_York" => "Eastern Standard Time",
                    "America/Chicago" => "Central Standard Time",
                    "America/Denver" => "Mountain Standard Time",
                    "America/Los_Angeles" => "Pacific Standard Time",
                    "Asia/Tokyo" => "Tokyo Standard Time",
                    "Asia/Shanghai" => "China Standard Time",
                    "UTC" => "UTC",
                    _ => "UTC"
                };
            }
            
            // On Unix systems, return the IANA ID as-is
            return ianaTimeZoneId;
        }
    }
}