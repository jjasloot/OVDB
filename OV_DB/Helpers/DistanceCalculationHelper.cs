using OVDB_database.Models;
using System;
using System.Linq;

namespace OV_DB.Helpers
{
    public class DistanceCalculationHelper
    {
        public static void ComputeDistance(Route route)
        {
            var coordinates = route.LineString.Coordinates
                .ToList();
            var distance = 0d;
            for (var index = 1; index < coordinates.Count; index++)
            {
                distance += GetDistanceInMetres(
                    coordinates[index - 1].Y, coordinates[index - 1].X,
                    coordinates[index].Y, coordinates[index].X);
            }
            distance = Math.Round(distance / 1000, 3);
            route.CalculatedDistance = Math.Round(distance * 1.0064, 2);

        }

        // Haversine great-circle distance, same formula and Earth radius as the retired
        // GeoCoordinate.NetStandard1 package so calculated route distances stay identical
        private static double GetDistanceInMetres(double lat1, double lon1, double lat2, double lon2)
        {
            var d1 = lat1 * (Math.PI / 180.0);
            var num1 = lon1 * (Math.PI / 180.0);
            var d2 = lat2 * (Math.PI / 180.0);
            var num2 = lon2 * (Math.PI / 180.0) - num1;
            var d3 = Math.Pow(Math.Sin((d2 - d1) / 2.0), 2.0) +
                     Math.Cos(d1) * Math.Cos(d2) * Math.Pow(Math.Sin(num2 / 2.0), 2.0);
            return 6376500.0 * (2.0 * Math.Atan2(Math.Sqrt(d3), Math.Sqrt(1.0 - d3)));
        }
    }
}
