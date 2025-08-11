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
            // For user-entered times from datetime-local inputs, treat them as local times
            // at the trip location. No timezone conversion needed - the user enters the 
            // actual times they want to record for the trip.
            return (endTime - startTime).TotalHours;
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