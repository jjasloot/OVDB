using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OV_DB.Services;
using OVDB_database.Database;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ImagesController(OVDBDatabaseContext context, IMemoryCache memoryCache, IFontLoader fontLoader) : ControllerBase
{
    private const int Padding = 12;
    private const int ComfortableRowHeight = 24;
    private const int CompactRowHeight = 18;
    private const int TitleAdvance = 22;
    private const int FooterHeight = 14;
    private const int BarHeight = 3;
    private const int DotRadius = 4;

    /// <summary>Colours for one badge theme.</summary>
    internal sealed record Palette(Color Background, Color Text, Color Muted, Color Divider, Color Track);

    internal static Palette GetPalette(string theme) => (theme ?? string.Empty).ToLowerInvariant() switch
    {
        "dark" => new Palette(
            Background: Color.ParseHex("1E1F22"),
            Text: Color.ParseHex("ECECEC"),
            Muted: Color.ParseHex("9AA0A6"),
            Divider: Color.ParseHex("3C4043"),
            Track: Color.ParseHex("2E3033")),
        // Keeps the original see-through behaviour for anyone already embedding the badge.
        "transparent" => new Palette(
            Background: Color.Transparent,
            Text: Color.ParseHex("1B1B1B"),
            Muted: Color.ParseHex("5F6368"),
            Divider: Color.ParseHex("DADCE0"),
            Track: Color.ParseHex("ECEDEF")),
        _ => new Palette(
            Background: Color.ParseHex("FFFFFF"),
            Text: Color.ParseHex("1B1B1B"),
            Muted: Color.ParseHex("5F6368"),
            Divider: Color.ParseHex("E3E5E8"),
            Track: Color.ParseHex("EFF1F3")),
    };

    [HttpGet]
    public async Task<ActionResult> GetImageAsync([FromQuery] List<Guid> guid, [FromQuery] int width = 420, [FromQuery] int height = 220, [FromQuery] string title = null, [FromQuery] bool includeTotal = false, [FromQuery] string language = "NL", [FromQuery] bool hideAttribution = false, [FromQuery] string theme = "light")
    {
        // This endpoint is intentionally anonymous (embeddable badge). Clamp the
        // attacker-controlled dimensions so a request cannot ask for a multi-GB
        // allocation, and cap the title length so it can't blow up the cache key.
        width = Math.Clamp(width, 20, 2000);
        height = Math.Clamp(height, 20, 2000);
        if (title is { Length: > 100 })
        {
            title = title[..100];
        }

        // Only render for maps that are actually shared, so the badge can't be used
        // to read stats for arbitrary (private) map GUIDs by enumeration.
        var sharedGuids = await context.Maps
            .Where(m => guid.Contains(m.MapGuid) && !string.IsNullOrWhiteSpace(m.SharingLinkName))
            .Select(m => m.MapGuid)
            .ToListAsync();
        if (sharedGuids.Count == 0)
        {
            return NotFound();
        }

        var id = "image|" + string.Join(',', sharedGuids.Select(g => g.ToString())) + "|" + width + "|" + height + "|" + includeTotal + "|" + title + "|" + language + "|" + hideAttribution + "|" + theme;

        var fileContents = await memoryCache.GetOrCreateAsync(id, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
            return await GenerateImageAsync(width, height, title, sharedGuids, includeTotal, language, hideAttribution, theme);
        });
        return File(fileContents, "image/png");
    }

    internal sealed record TypeRow(string Name, Color Colour, double YearDistance, double MonthDistance);

    private async Task<byte[]> GenerateImageAsync(int width, int height, string title, List<Guid> guids, bool includeTotal, string language, bool hideAttribution, string theme)
    {
        var now = DateTime.Now;
        var raw = await context.RouteInstances
            .AsNoTracking()
            .Where(ri => ri.Route.RouteMaps.Any(rm => guids.Contains(rm.Map.MapGuid)) || ri.RouteInstanceMaps.Any(rim => guids.Contains(rim.Map.MapGuid)))
            .Where(ri => ri.Date.Year == now.Year)
            .Select(ri => new
            {
                ri.Date,
                ri.Route.RouteType.Name,
                ri.Route.RouteType.NameNL,
                ri.Route.RouteType.Colour,
                Distance = (double)((ri.Route.OverrideDistance.HasValue && ri.Route.OverrideDistance > 0) ? ri.Route.OverrideDistance : ri.Route.CalculatedDistance)
            })
            .ToListAsync();

        var dutch = string.Equals(language, "NL", StringComparison.OrdinalIgnoreCase);
        var palette = GetPalette(theme);

        var rows = raw
            .GroupBy(r => r.Name)
            .Select(g => new TypeRow(
                Name: dutch && !string.IsNullOrWhiteSpace(g.First().NameNL) ? g.First().NameNL : g.Key,
                Colour: ParseColour(g.First().Colour, palette.Muted),
                YearDistance: Math.Round(g.Sum(v => v.Distance), 1),
                MonthDistance: Math.Round(g.Where(v => v.Date.Month == now.Month).Sum(v => v.Distance), 1)))
            .OrderByDescending(r => r.YearDistance)
            .ToList();

        return RenderBadge(rows, fontLoader.FontCollection.Get("Ubuntu"), width, height, title, includeTotal, dutch, hideAttribution, palette, now);
    }

    /// <summary>
    /// Pure rendering step: turns already-aggregated rows into the badge PNG. Kept separate from
    /// the data query so the layout can be exercised without a database.
    /// </summary>
    internal static byte[] RenderBadge(IReadOnlyList<TypeRow> rows, FontFamily family, int width, int height, string title, bool includeTotal, bool dutch, bool hideAttribution, Palette palette, DateTime now)
    {
        var culture = dutch ? CultureInfo.GetCultureInfo("nl-NL") : CultureInfo.GetCultureInfo("en-GB");
        var titleFont = new Font(family, 15, FontStyle.Bold);
        var rowFont = new Font(family, 12);
        var valueFont = new Font(family, 12, FontStyle.Bold);
        // Deliberately smaller than a data row: the overflow count is metadata, not a figure.
        var overflowFont = new Font(family, 10);
        var footerFont = new Font(family, 9);

        var textBrush = Brushes.Solid(palette.Text);
        var mutedBrush = Brushes.Solid(palette.Muted);

        using var image = new Image<Rgba32>(width, height);
        if (palette.Background != Color.Transparent)
        {
            image.Mutate(ctx => ctx.BackgroundColor(palette.Background));
        }

        var top = Padding;
        if (!string.IsNullOrWhiteSpace(title))
        {
            image.Mutate(ctx => ctx.DrawText(title, titleFont, textBrush, new PointF(Padding, top)));
            top += TitleAdvance;
        }

        var footerSpace = hideAttribution ? 0 : FooterHeight;
        var totalSpace = includeTotal ? ComfortableRowHeight + 6 : 0;
        var available = Math.Max(CompactRowHeight, height - top - footerSpace - totalSpace - Padding);

        // Prefer roomy rows with a small bar chart, but fall back to a dense list rather than
        // dropping most of the data when the caller asked for a short image.
        var rowHeight = ComfortableRowHeight;
        var showBars = true;
        var rowsPerColumn = Math.Max(1, available / rowHeight);
        if (rowsPerColumn < Math.Min(rows.Count, 3))
        {
            rowHeight = CompactRowHeight;
            showBars = false;
            rowsPerColumn = Math.Max(1, available / rowHeight);
        }

        string FormatKm(double value) => string.Format(culture, "{0:N0} km", value);

        var labelWidth = rows.Count == 0 ? 0f : rows.Max(r => TextMeasurer.MeasureAdvance(r.Name, new TextOptions(rowFont)).Width);
        var yearWidth = rows.Count == 0 ? 0f : rows.Max(r => TextMeasurer.MeasureAdvance(FormatKm(r.YearDistance), new TextOptions(valueFont)).Width);
        var monthWidth = rows.Count == 0 ? 0f : rows.Max(r => TextMeasurer.MeasureAdvance(FormatKm(r.MonthDistance), new TextOptions(rowFont)).Width);
        var columnWidth = (DotRadius * 2) + 8 + labelWidth + 12 + yearWidth + 10 + monthWidth + 16;
        var usableWidth = width - (Padding * 2);
        var columns = Math.Max(1, (int)(usableWidth / Math.Max(columnWidth, 1)));
        columnWidth = Math.Min(columnWidth, usableWidth / (float)columns);

        // When not everything fits, give up one slot so the overflow can be stated as a normal
        // list row ("+2 more") instead of a marker squeezed in beside the total or the footer.
        // Only give up a slot when at least two real rows still remain; on a very short badge the
        // count falls back to the attribution instead, rather than crowding out the data.
        var capacity = rowsPerColumn * columns;
        var useOverflowRow = rows.Count > capacity && capacity >= 3;
        var shownCount = useOverflowRow ? capacity - 1 : Math.Min(capacity, rows.Count);
        var visible = rows.Take(shownCount).ToList();
        var hidden = rows.Count - visible.Count;
        var overflowRowIndex = useOverflowRow ? shownCount : -1;
        var maxDistance = visible.Count == 0 ? 0 : visible.Max(r => r.YearDistance);

        for (var index = 0; index < visible.Count; index++)
        {
            var row = visible[index];
            var x = Padding + ((index / rowsPerColumn) * columnWidth);
            var y = top + ((index % rowsPerColumn) * rowHeight);
            var textY = y + 1;

            var yearText = FormatKm(row.YearDistance);
            var monthText = FormatKm(row.MonthDistance);
            var yearRight = x + columnWidth - monthWidth - 16;
            var monthRight = x + columnWidth - 8;

            image.Mutate(ctx =>
            {
                // Colour dot in the route type's own colour, so the badge reads at a glance.
                ctx.Fill(row.Colour, new EllipsePolygon(x + DotRadius, textY + 8, DotRadius));
                ctx.DrawText(row.Name, rowFont, textBrush, new PointF(x + (DotRadius * 2) + 8, textY));
                ctx.DrawText(yearText, valueFont, textBrush,
                    new PointF(yearRight - TextMeasurer.MeasureAdvance(yearText, new TextOptions(valueFont)).Width, textY));
                ctx.DrawText(monthText, rowFont, mutedBrush,
                    new PointF(monthRight - TextMeasurer.MeasureAdvance(monthText, new TextOptions(rowFont)).Width, textY));
            });

            // A thin proportional bar under each row turns the list into a small chart. It sits
            // below the text baseline so it reads as a bar, not as an underline.
            if (showBars && maxDistance > 0)
            {
                var barTop = y + rowHeight - BarHeight - 2;
                var barMaxWidth = Math.Max(4f, columnWidth - 16);
                var barWidth = (float)Math.Max(2, barMaxWidth * (row.YearDistance / maxDistance));
                image.Mutate(ctx =>
                {
                    ctx.Fill(palette.Track, new RectangularPolygon(x, barTop, barMaxWidth, BarHeight));
                    ctx.Fill(row.Colour, new RectangularPolygon(x, barTop, barWidth, BarHeight));
                });
            }
        }

        // Overflow reads as an ordinary, muted list row aligned with the ones above it.
        if (overflowRowIndex >= 0)
        {
            var x = Padding + ((overflowRowIndex / rowsPerColumn) * columnWidth);
            var y = top + ((overflowRowIndex % rowsPerColumn) * rowHeight);
            var moreText = dutch ? $"+{hidden} meer" : $"+{hidden} more";
            image.Mutate(ctx => ctx.DrawText(moreText, overflowFont, mutedBrush,
                new PointF(x + (DotRadius * 2) + 8, y + 3)));
        }

        var footerTop = height - Padding - footerSpace;
        if (includeTotal && rows.Count > 0)
        {
            var totalYear = Math.Round(rows.Sum(r => r.YearDistance), 1);
            var totalMonth = Math.Round(rows.Sum(r => r.MonthDistance), 1);
            var lineY = footerTop - ComfortableRowHeight - 2;
            image.Mutate(ctx => ctx.DrawLine(palette.Divider, 1, new PointF(Padding, lineY), new PointF(width - Padding, lineY)));

            var label = dutch ? "Totaal" : "Total";
            var totalText = FormatKm(totalYear);
            var totalMonthText = FormatKm(totalMonth);
            var labelY = lineY + 5;
            image.Mutate(ctx =>
            {
                ctx.DrawText(label, valueFont, textBrush, new PointF(Padding, labelY));
                ctx.DrawText(totalText, valueFont, textBrush,
                    new PointF(width - Padding - monthWidth - 16 - TextMeasurer.MeasureAdvance(totalText, new TextOptions(valueFont)).Width, labelY));
                ctx.DrawText(totalMonthText, rowFont, mutedBrush,
                    new PointF(width - Padding - 8 - TextMeasurer.MeasureAdvance(totalMonthText, new TextOptions(rowFont)).Width, labelY));
            });
        }

        if (!hideAttribution)
        {
            var stamp = now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            // Only when even the overflow row could not be shown (a single-row image) does the
            // count fall back to the attribution, rather than being dropped silently.
            var attribution = hidden > 0 && overflowRowIndex < 0 ? $"ovdb.infinityx.nl  +{hidden}" : "ovdb.infinityx.nl";
            image.Mutate(ctx =>
            {
                ctx.DrawText(attribution, footerFont, mutedBrush, new PointF(Padding, footerTop + 1));
                ctx.DrawText(stamp, footerFont, mutedBrush,
                    new PointF(width - Padding - TextMeasurer.MeasureAdvance(stamp, new TextOptions(footerFont)).Width, footerTop + 1));
            });
        }

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static Color ParseColour(string colour, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(colour))
        {
            return fallback;
        }
        try
        {
            return Color.ParseHex(colour.TrimStart('#'));
        }
        catch (ArgumentException)
        {
            return fallback;
        }
    }
}
