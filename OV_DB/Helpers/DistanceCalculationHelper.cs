using OVDB_database.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Helpers
{
    public class DistanceCalculationHelper
    {
        public static void ComputeDistance(Route route)
        {
            var coordinates = route.Coordinates
                .Split('\n')
                .ToList()
                .Select(c => c.Split(',').Select(c => double.Parse(c, CultureInfo.InvariantCulture)).ToList())
                .ToList();
            var distance = 0d;
            for (var index = 1; index < coordinates.Count; index++)
            {
                var additionalDistance = Distance(coordinates[index - 1][1], coordinates[index - 1][0], coordinates[index][1], coordinates[index][0]);
                distance += additionalDistance;
            }
            distance = Math.Round(distance, 3);
            route.CalculatedDistance = distance;

        }

        private static double Distance(double lat1, double lon1, double lat2, double lon2)
        {
            double rad(double angle) => angle * 0.017453292519943295769236907684886127d; // = angle * Math.Pi / 180.0d
            double havf(double diff) => Math.Pow(Math.Sin(rad(diff) / 2d), 2); // = sin²(diff / 2)
            return 12745.6 * Math.Asin(Math.Sqrt(havf(lat2 - lat1) + Math.Cos(rad(lat1)) * Math.Cos(rad(lat2)) * havf(lon2 - lon1))); // earth radius 6.372,8‬km x 2 = 12745.6

        }

        private static double Deg2Rad(double deg)
        {
            return (deg * Math.PI / 180.0);
        }

        private static double Rad2Deg(double rad)
        {
            return (rad / Math.PI * 180.0);
        }

    }
}
