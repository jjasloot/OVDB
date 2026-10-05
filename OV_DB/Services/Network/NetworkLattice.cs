using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;

namespace OV_DB.Services.Network;

/// <summary>
/// The one definition of the coverage lattice, shared by assembly (which stores
/// <c>NetworkEdge.SampleCount</c>), the matcher (which sets bits) and the completion read path
/// (which turns bits into intervals). Sample i sits at chainage min(i · 25 m, L); the count is
/// floor(L / 25) + 1, plus one more sample at L when L is not a lattice multiple.
/// </summary>
public static class NetworkLattice
{
    public const double SpacingM = 25;

    /// <summary>Sample points on the lattice over a projected (metres) polyline.</summary>
    public static Coordinate[] Sample(Coordinate[] projected)
    {
        var cumulative = Cumulative(projected);
        var total = cumulative[^1];
        var samples = new Coordinate[SampleCount(total)];
        var seg = 1;
        for (var i = 0; i < samples.Length; i++)
        {
            var chainage = Math.Min(i * SpacingM, total);
            while (seg < projected.Length - 1 && cumulative[seg] < chainage)
                seg++;
            var segLen = cumulative[seg] - cumulative[seg - 1];
            var t = segLen <= 0 ? 0 : (chainage - cumulative[seg - 1]) / segLen;
            samples[i] = new Coordinate(
                projected[seg - 1].X + (projected[seg].X - projected[seg - 1].X) * t,
                projected[seg - 1].Y + (projected[seg].Y - projected[seg - 1].Y) * t);
        }
        return samples;
    }

    public static int SampleCount(double totalM)
    {
        var count = (int)Math.Floor(totalM / SpacingM) + 1;
        var needsEnd = totalM - (count - 1) * SpacingM > 1e-6;
        return count + (needsEnd ? 1 : 0);
    }

    public static double PolylineLength(Coordinate[] projected) => Cumulative(projected)[^1];

    private static double[] Cumulative(Coordinate[] coords)
    {
        var cumulative = new double[coords.Length];
        for (var i = 1; i < coords.Length; i++)
            cumulative[i] = cumulative[i - 1] + coords[i - 1].Distance(coords[i]);
        return cumulative;
    }

    // ---- bitmaps ------------------------------------------------------------------------------

    public static byte[] ToBytes(bool[] bits)
    {
        var bytes = new byte[(bits.Length + 7) / 8];
        for (var i = 0; i < bits.Length; i++)
            if (bits[i])
                bytes[i >> 3] |= (byte)(1 << (i & 7));
        return bytes;
    }

    public static bool[] ToBits(byte[] bytes, int sampleCount)
    {
        var bits = new bool[sampleCount];
        for (var i = 0; i < sampleCount && (i >> 3) < bytes.Length; i++)
            bits[i] = (bytes[i >> 3] & (1 << (i & 7))) != 0;
        return bits;
    }

    public static void OrInto(bool[] target, byte[] bytes)
    {
        for (var i = 0; i < target.Length && (i >> 3) < bytes.Length; i++)
            if ((bytes[i >> 3] & (1 << (i & 7))) != 0)
                target[i] = true;
    }

    public static int PopCount(bool[] bits)
    {
        var n = 0;
        foreach (var b in bits)
            if (b) n++;
        return n;
    }

    // ---- intervals ----------------------------------------------------------------------------

    /// <summary>
    /// Run-length encode covered samples into [start, end] fractions of the edge's full length.
    /// A run of samples a..b covers the line between them plus half a spacing on each side, so a
    /// fully covered edge yields exactly [0, 1].
    /// </summary>
    public static List<double[]> RunsToIntervals(bool[] bits, double totalM)
    {
        var result = new List<double[]>();
        if (totalM <= 0)
            return result;
        var i = 0;
        while (i < bits.Length)
        {
            if (!bits[i]) { i++; continue; }
            var j = i;
            while (j + 1 < bits.Length && bits[j + 1]) j++;
            var startM = Math.Max(0, Math.Min(i * SpacingM, totalM) - SpacingM / 2);
            var endM = Math.Min(totalM, Math.Min(j * SpacingM, totalM) + SpacingM / 2);
            result.Add([startM / totalM, endM / totalM]);
            i = j + 1;
        }
        return result;
    }
}

/// <summary>
/// Piecewise-linear mapping from a fraction of the full geometry's length to a fraction of the
/// simplified geometry's length, anchored at the vertices Douglas-Peucker kept (it preserves
/// original vertex coordinates exactly, so the mapping is lossless). Needed because a curvy half
/// of an edge loses more length in simplification than a straight half — measured worst edges
/// at 94 % — so a full-length fraction would land visibly off on the displayed polyline.
/// </summary>
public sealed class SimplifiedChainage
{
    private readonly double[] _fullAtKept;
    private readonly double[] _simpCum;
    private readonly double _fullTotal;
    private readonly double _simpTotal;

    public SimplifiedChainage(Coordinate[] full, Coordinate[] simplified)
    {
        var fullCum = new double[full.Length];
        for (var i = 1; i < full.Length; i++)
            fullCum[i] = fullCum[i - 1] + NetworkGeo.HaversineKm(full[i - 1], full[i]);
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
                _simpCum[k] = _simpCum[k - 1] + NetworkGeo.HaversineKm(simplified[k - 1], simplified[k]);
        }
        _simpTotal = _simpCum[^1];
    }

    public double ToSimplifiedFraction(double fullFraction)
    {
        if (_fullTotal <= 0 || _simpTotal <= 0 || _fullAtKept.Length < 2)
            return Math.Clamp(fullFraction, 0, 1);
        var c = Math.Clamp(fullFraction, 0, 1) * _fullTotal;
        var k = 1;
        while (k < _fullAtKept.Length - 1 && _fullAtKept[k] < c)
            k++;
        var span = _fullAtKept[k] - _fullAtKept[k - 1];
        var t = span <= 0 ? 0 : Math.Clamp((c - _fullAtKept[k - 1]) / span, 0, 1);
        return Math.Clamp((_simpCum[k - 1] + t * (_simpCum[k] - _simpCum[k - 1])) / _simpTotal, 0, 1);
    }
}
