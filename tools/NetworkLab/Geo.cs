using NetTopologySuite.Geometries;

namespace NetworkLab;

public static class Geo
{
    /// <summary>
    /// Same haversine formula and Earth radius as OV_DB.Helpers.DistanceCalculationHelper, so lab
    /// numbers are comparable with route distances. No 1.0064 fudge factor: that is a route-import
    /// calibration, network lengths stay raw.
    /// </summary>
    public static double HaversineKm(Coordinate a, Coordinate b)
    {
        var d1 = a.Y * (Math.PI / 180.0);
        var num1 = a.X * (Math.PI / 180.0);
        var d2 = b.Y * (Math.PI / 180.0);
        var num2 = b.X * (Math.PI / 180.0) - num1;
        var d3 = Math.Pow(Math.Sin((d2 - d1) / 2.0), 2.0) +
                 Math.Cos(d1) * Math.Cos(d2) * Math.Pow(Math.Sin(num2 / 2.0), 2.0);
        return 6376.5 * (2.0 * Math.Atan2(Math.Sqrt(d3), Math.Sqrt(1.0 - d3)));
    }

    public static double LengthKm(IReadOnlyList<Coordinate> coords, int start, int endExclusive)
    {
        var km = 0d;
        for (var i = Math.Max(start, 0) + 1; i < endExclusive; i++)
            km += HaversineKm(coords[i - 1], coords[i]);
        return km;
    }

    private const double MetresPerDegree = 111_320;

    /// <summary>
    /// Local equirectangular frame: planar coordinates in metres, accurate to well under a percent
    /// at region scale. All matching runs in this frame so NTS planar distances are real metres.
    /// </summary>
    public sealed class LocalProjection(double lat0)
    {
        private readonly double _xScale = Math.Cos(lat0 * Math.PI / 180.0) * MetresPerDegree;

        public Coordinate Project(Coordinate c) => new(c.X * _xScale, c.Y * MetresPerDegree);

        public Coordinate Unproject(Coordinate c) => new(c.X / _xScale, c.Y / MetresPerDegree);

        public Coordinate[] Project(Coordinate[] coords)
        {
            var result = new Coordinate[coords.Length];
            for (var i = 0; i < coords.Length; i++)
                result[i] = Project(coords[i]);
            return result;
        }
    }
}
