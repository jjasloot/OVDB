using NetTopologySuite.Geometries;
using System.Text;
using System;

namespace OV_DB.Helpers
{
    public static class PolylineHelper
    {
        public static string Encode(LineString lineString)
        {
            if (lineString == null)
            {
                return null;
            }

            var str = new StringBuilder();

            var encodeDiff = (Action<int>)(diff =>
            {
                int shifted = diff << 1;
                if (diff < 0)
                    shifted = ~shifted;

                int rem = shifted;

                while (rem >= 0x20)
                {
                    str.Append((char)((0x20 | (rem & 0x1f)) + 63));
                    rem >>= 5;
                }

                str.Append((char)(rem + 63));
            });

            int lastLat = 0;
            int lastLng = 0;

            // Google Polyline uses Lat, Lng order.
            // NetTopologySuite Coordinate is X (Lng), Y (Lat).
            foreach (var point in lineString.Coordinates)
            {
                int lat = (int)Math.Round(point.Y * 1E5);
                int lng = (int)Math.Round(point.X * 1E5);

                encodeDiff(lat - lastLat);
                encodeDiff(lng - lastLng);

                lastLat = lat;
                lastLng = lng;
            }

            return str.ToString();
        }
    }
}
