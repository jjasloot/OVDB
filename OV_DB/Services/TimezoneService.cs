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
            var startTimezoneId = GetTimezoneId(lineString[0].Y, lineString[0].X);
            var end = lineString[lineString.Count - 1];
            var endTimezoneId = GetTimezoneId(end.Y, end.X);

            var startTimezone = GetTimeZoneInfo(startTimezoneId);
            var endTimezone = GetTimeZoneInfo(endTimezoneId);
            startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Unspecified);
            endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Unspecified);
            //convert start and endtime to utc 
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(startTime, startTimezone);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(endTime, endTimezone);
            //calculate duration in hours

            return (endUtc - startUtc).TotalHours;


        }

        private string GetTimezoneId(double latitude, double longitude)
        {
            // No longer needed - keeping for future use if geographic timezone lookup is required
            try
            {
                return TimeZoneLookup.GetTimeZone(latitude, longitude).Result;
            }
            catch
            {
                return "UTC";
            }
        }

        private TimeZoneInfo GetTimeZoneInfo(string ianaTimeZoneId)
        {
            // No longer needed - keeping for future use if timezone conversion is required
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(ianaTimeZoneId);
            }
            catch
            {
                try
                {
                    var windowsId = TZConvert.IanaToWindows(ianaTimeZoneId);
                    return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                }
                catch
                {
                    return TimeZoneInfo.Utc;
                }
            }
        }
    }
}