using NetTopologySuite.Features;
using NetTopologySuite.IO;
using NetTopologySuite.Simplify;

namespace NetworkLab;

/// <summary>
/// Piecewise-linear mapping from a fraction of the full geometry's length to a fraction of the
/// simplified geometry's length, anchored at the vertices the simplifier kept. This is what the
/// completion endpoint will apply before emitting intervals (design 6).
/// </summary>
public sealed class SimplifiedChainage
{
    private readonly double[] _fullAtKept;   // full-geometry chainage of each kept vertex
    private readonly double[] _simpCum;      // simplified-geometry chainage of each kept vertex
    private readonly double _fullTotal;
    private readonly double _simpTotal;

    public SimplifiedChainage(NetTopologySuite.Geometries.Coordinate[] full, NetTopologySuite.Geometries.Coordinate[] simplified)
    {
        var fullCum = new double[full.Length];
        for (var i = 1; i < full.Length; i++)
            fullCum[i] = fullCum[i - 1] + Geo.HaversineKm(full[i - 1], full[i]);
        _fullTotal = fullCum[^1];

        _fullAtKept = new double[simplified.Length];
        _simpCum = new double[simplified.Length];
        var fi = 0;
        for (var k = 0; k < simplified.Length; k++)
        {
            while (fi < full.Length - 1 && !full[fi].Equals2D(simplified[k]))
                fi++;
            _fullAtKept[k] = fullCum[fi];
            if (k > 0)
                _simpCum[k] = _simpCum[k - 1] + Geo.HaversineKm(simplified[k - 1], simplified[k]);
        }
        _simpTotal = _simpCum[^1];
    }

    public double ToSimplifiedFraction(double fullFraction)
    {
        if (_fullTotal <= 0 || _simpTotal <= 0)
            return fullFraction;
        var c = Math.Clamp(fullFraction, 0, 1) * _fullTotal;
        var k = 1;
        while (k < _fullAtKept.Length - 1 && _fullAtKept[k] < c)
            k++;
        var span = _fullAtKept[k] - _fullAtKept[k - 1];
        var t = span <= 0 ? 0 : Math.Clamp((c - _fullAtKept[k - 1]) / span, 0, 1);
        return Math.Clamp((_simpCum[k - 1] + t * (_simpCum[k] - _simpCum[k - 1])) / _simpTotal, 0, 1);
    }
}

public static class GeoJsonExport
{
    /// <summary>
    /// One FeatureCollection per mode, geometry simplified to ~10 m for the viewer. Properties:
    /// name, km, passenger, and the best covered fraction per tolerance (f10/f15/f20/f30).
    /// </summary>
    public static void Write(string dataDir, int regionId, NetworkMode mode, List<NetworkEdgeDraft> edges)
    {
        var collection = new FeatureCollection();
        // Partial rendering cuts the SIMPLIFIED polyline by length fraction, so its length must
        // track the full geometry (design: within ~1 %). Measure that here.
        double fullKm = 0, simplifiedKm = 0, worstRatio = 1;
        foreach (var edge in edges)
        {
            var attributes = new AttributesTable
            {
                { "name", edge.Name ?? "" },
                { "km", Math.Round(edge.LengthKm, 3) },
                { "passenger", edge.IsPassenger },
            };
            var simplified = DouglasPeuckerSimplifier.Simplify(edge.Geometry, 0.0001);
            var remap = new SimplifiedChainage(edge.Geometry.Coordinates, simplified.Coordinates);
            for (var t = 0; t < Matcher.Tolerances.Length; t++)
            {
                attributes.Add($"f{(int)Matcher.Tolerances[t]}", Math.Round(edge.BestFraction?[t] ?? 0, 3));
                // Intervals leave the matcher as fractions of the FULL geometry's length; the client
                // cuts the SIMPLIFIED polyline, whose length is not proportional along the edge
                // (a curvy half loses more than a straight half - worst edges measured at 94-98 %).
                // Douglas-Peucker keeps a subset of the original vertices, so the mapping between
                // the two chainages is exact through those vertices.
                var mapped = (edge.Intervals?[t] ?? new List<double[]>())
                    .Select(iv => new[] { Math.Round(remap.ToSimplifiedFraction(iv[0]), 3), Math.Round(remap.ToSimplifiedFraction(iv[1]), 3) })
                    .ToList();
                attributes.Add($"i{(int)Matcher.Tolerances[t]}", mapped);
            }
            var simpKm = Geo.LengthKm(simplified.Coordinates, 0, simplified.Coordinates.Length);
            fullKm += edge.LengthKm;
            simplifiedKm += simpKm;
            if (edge.LengthKm >= 0.5)
                worstRatio = Math.Min(worstRatio, simpKm / edge.LengthKm);
            collection.Add(new Feature(simplified, attributes));
        }

        var path = Path.Combine(dataDir, $"network-{regionId}-{mode}.geojson".ToLowerInvariant());
        File.WriteAllText(path, new GeoJsonWriter().Write(collection));
        Console.WriteLine($"  wrote {path} ({edges.Count} edges)");
        Console.WriteLine($"  simplified/full length: {simplifiedKm / fullKm:P3} overall, worst edge >= 500 m: {worstRatio:P2}");
    }
}
