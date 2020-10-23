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
                var start = new GeoCoordinatePortable.GeoCoordinate(coordinates[index - 1][1], coordinates[index - 1][0]);
                var end = new GeoCoordinatePortable.GeoCoordinate(coordinates[index][1], coordinates[index][0]);

                distance += start.GetDistanceTo(end);
            }
            distance = Math.Round(distance / 1000, 3);
            route.CalculatedDistance = distance*1.0064;

        }

    }
}
