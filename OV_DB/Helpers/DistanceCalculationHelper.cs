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
                var start = new GeoCoordinatePortable.GeoCoordinate(coordinates[index - 1].X, coordinates[index - 1].Y);
                var end = new GeoCoordinatePortable.GeoCoordinate(coordinates[index].X, coordinates[index].Y);

                distance += start.GetDistanceTo(end);
            }
            distance = Math.Round(distance / 1000, 3);
            route.CalculatedDistance = Math.Round(distance * 1.0064, 2);

        }

    }
}
