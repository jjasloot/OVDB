namespace OV_DB.Services;
using System;
using System.Text;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using NetTopologySuite.Simplify;
using System.Linq;
using System.Globalization;
using System.Drawing;

public class CountrySvgGenerator
{
    private readonly int width;
    private readonly int height;
    private readonly double padding;
    private readonly double simplificationTolerance;
    private readonly Random random;

    public CountrySvgGenerator(
        int width = 800,
        int height = 600,
        double padding = 0.1,
        double simplificationTolerance = 0.01)
    {
        this.width = width;
        this.height = height;
        this.padding = padding;
        this.simplificationTolerance = simplificationTolerance;
        this.random = new Random();
    }

    private string GenerateRandomColor()
    {
        // Generate pleasant, muted colors
        int r = random.Next(100, 200);
        int g = random.Next(100, 200);
        int b = random.Next(100, 200);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    public string GenerateSvg(IEnumerable<(string CountryName, Geometry Geometry, bool Visited)> countries)
    {
        var sb = new StringBuilder();

        var simplifiedCountries = countries.Select(c => (
            c.CountryName,
            SimplifyGeometry(c.Geometry),
            c.Visited,
            Color: GenerateRandomColor()
        )).ToList();

        var envelope = CalculateTotalBounds(simplifiedCountries);
        var transform = CalculateTransform(envelope);

        sb.AppendFormat(CultureInfo.InvariantCulture,
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {0} {1}\">",
            width, height);
        sb.AppendLine();

        sb.AppendLine(@"<style>
            .country { 
                stroke: #666666; 
                stroke-width: 0.5; 
                transition: opacity 0.3s;
            }
            .country:hover { 
                opacity: 0.8;
            }
            .unvisited {
                fill: transparent !important;
            }
        </style>");

        foreach (var (countryName, geometry, visited, color) in simplifiedCountries)
        {
            string pathData = GeneratePathData(geometry, transform);
            string className = visited ? "country" : "country unvisited";

            sb.AppendFormat(CultureInfo.InvariantCulture,
                "<path class=\"{0}\" d=\"{1}\" data-name=\"{2}\" style=\"fill: {3}\">",
                className, pathData, countryName, color).AppendLine();
            sb.AppendFormat(CultureInfo.InvariantCulture, "<title>{0}</title>", countryName).AppendLine();
            sb.AppendLine("</path>");
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private Geometry SimplifyGeometry(Geometry geometry)
    {
        var simplifier = new DouglasPeuckerSimplifier(geometry)
        {
            DistanceTolerance = simplificationTolerance,
            EnsureValidTopology = true
        };
        return simplifier.GetResultGeometry();
    }

    private (double ScaleX, double ScaleY, double TranslateX, double TranslateY) CalculateTransform(Envelope envelope)
    {
        double dataWidth = envelope.Width;
        double dataHeight = envelope.Height;

        double scaleX = width * (1 - 2 * padding) / dataWidth;
        double scaleY = height * (1 - 2 * padding) / dataHeight;

        double scale = Math.Min(scaleX, scaleY);

        double translateX = -envelope.MinX * scale + (width - scale * dataWidth) / 2;
        double translateY = height - (-envelope.MinY * scale + (height - scale * dataHeight) / 2);

        return (scale, -scale, translateX, translateY);
    }

    private Envelope CalculateTotalBounds(List<(string CountryName, Geometry Geometry, bool Visited, string Color)> geometries)
    {
        var envelope = new Envelope();
        foreach (var geometry in geometries.Where(g=>g.Visited))
        {
            envelope.ExpandToInclude(geometry.Geometry.EnvelopeInternal);
        }
        return envelope;
    }

    private string GeneratePathData(Geometry geometry, (double ScaleX, double ScaleY, double TranslateX, double TranslateY) transform)
    {
        var pathBuilder = new StringBuilder();

        if (geometry is Polygon polygon)
        {
            AppendPolygon(pathBuilder, polygon, transform);
        }
        else if (geometry is MultiPolygon multiPolygon)
        {
            for (int i = 0; i < multiPolygon.NumGeometries; i++)
            {
                if (i > 0) pathBuilder.Append(" ");
                AppendPolygon(pathBuilder, (Polygon)multiPolygon.GetGeometryN(i), transform);
            }
        }

        return pathBuilder.ToString();
    }

    private void AppendPolygon(StringBuilder pathBuilder, Polygon polygon, (double ScaleX, double ScaleY, double TranslateX, double TranslateY) transform)
    {
        AppendLinearRing(pathBuilder, polygon.ExteriorRing, transform);

        for (int i = 0; i < polygon.NumInteriorRings; i++)
        {
            pathBuilder.Append(" ");
            AppendLinearRing(pathBuilder, polygon.GetInteriorRingN(i), transform);
        }
    }

    private void AppendLinearRing(StringBuilder pathBuilder, LineString ring, (double ScaleX, double ScaleY, double TranslateX, double TranslateY) transform)
    {
        var coordinates = ring.Coordinates;

        for (int i = 0; i < coordinates.Length; i++)
        {
            double x = coordinates[i].X * transform.ScaleX + transform.TranslateX;
            double y = coordinates[i].Y * transform.ScaleY + transform.TranslateY;

            if (i == 0)
                pathBuilder.AppendFormat(CultureInfo.InvariantCulture, "M {0:F2} {1:F2}", x, y);
            else
                pathBuilder.AppendFormat(CultureInfo.InvariantCulture, " L {0:F2} {1:F2}", x, y);
        }

        pathBuilder.Append(" Z");
    }

    public (int OriginalPoints, int SimplifiedPoints) GetSimplificationStats(Geometry original)
    {
        int originalPoints = CountPoints(original);
        int simplifiedPoints = CountPoints(SimplifyGeometry(original));
        return (originalPoints, simplifiedPoints);
    }

    private int CountPoints(Geometry geometry)
    {
        return geometry.Coordinates.Length;
    }
}