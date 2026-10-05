using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;

namespace OV_DB.Services.Network;

/// <summary>Geodesic lengths and the local planar frame the network pipeline computes in.</summary>
public static class NetworkGeo
{
    /// <summary>
    /// Haversine with the same Earth radius as <see cref="OV_DB.Helpers.DistanceCalculationHelper"/>,
    /// so network lengths are comparable with route distances. Deliberately without that helper's
    /// 1.0064 factor: that is a route-import calibration, network lengths stay raw.
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

    public static double LengthKm(IReadOnlyList<Coordinate> coords) => LengthKm(coords, 0, coords.Count);

    /// <summary>
    /// A line as a GeoJSON LineString, coordinates [lon, lat] rounded to 6 decimals (~0.1 m).
    /// Written by hand on purpose: the MVC pipeline serialises with Newtonsoft, which renders an NTS
    /// geometry as its object graph (coordinate sequences, envelopes, NaN Z/M — several times the
    /// size and not GeoJSON at all), and rounding keeps the national network payload small.
    /// </summary>
    public static string ToGeoJson(LineString line)
    {
        var sb = new System.Text.StringBuilder(32 + line.NumPoints * 24);
        sb.Append("{\"type\":\"LineString\",\"coordinates\":[");
        var coords = line.Coordinates;
        for (var i = 0; i < coords.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('[')
              .Append(Math.Round(coords[i].X, 6).ToString("0.######", System.Globalization.CultureInfo.InvariantCulture))
              .Append(',')
              .Append(Math.Round(coords[i].Y, 6).ToString("0.######", System.Globalization.CultureInfo.InvariantCulture))
              .Append(']');
        }
        return sb.Append("]}").ToString();
    }

    private const double MetresPerDegree = 111_320;

    /// <summary>
    /// Local equirectangular frame: planar coordinates in metres, accurate to well under a percent
    /// at region scale. Dedup, splitting and matching all run in this frame so NTS planar
    /// distances are real metres.
    /// </summary>
    public sealed class LocalProjection(double lat0)
    {
        private readonly double _xScale = Math.Cos(lat0 * Math.PI / 180.0) * MetresPerDegree;

        public Coordinate Project(Coordinate c) => new(c.X * _xScale, c.Y * MetresPerDegree);

        public Coordinate Unproject(Coordinate c) => new(c.X / _xScale, c.Y / MetresPerDegree);

        public Coordinate[] Project(IReadOnlyList<Coordinate> coords)
        {
            var result = new Coordinate[coords.Count];
            for (var i = 0; i < coords.Count; i++)
                result[i] = Project(coords[i]);
            return result;
        }
    }
}
