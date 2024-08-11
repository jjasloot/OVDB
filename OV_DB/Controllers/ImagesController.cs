using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OV_DB.Services;
using OVDB_database.Database;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ImagesController(OVDBDatabaseContext context, IMemoryCache memoryCache, IFontLoader fontLoader) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetImageAsync([FromQuery] List<Guid> guid, [FromQuery] int width = 300, [FromQuery] int height = 100, [FromQuery] string title = null, [FromQuery] bool includeTotal = false, [FromQuery] string language = "NL")
    {
        var id = "image|" + string.Join(',', guid.Select(g => g.ToString())) + "|" + width + "|" + height + "|" + includeTotal + "|" + title + "|" + language;

        var fileContents = await memoryCache.GetOrCreateAsync(id, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
            return await GenerateImageAsync(width, height, title, guid, includeTotal, language);
        });
        //var fileContents = await GenerateImageAsync(width, height, title, guid, includeTotal);
        return File(fileContents, "image/png");
    }

    private async Task<byte[]> GenerateImageAsync(int width, int height, string title, List<Guid> guids, bool includeTotal, string language)
    {
        var query = context.RouteInstances
 .Where(ri => ri.Route.RouteMaps.Any(rm => guids.Contains(rm.Map.MapGuid)) || ri.RouteInstanceMaps.Any(rim => guids.Contains(rim.Map.MapGuid)));

        query = query.Where(ri => ri.Date.Year == DateTime.Now.Year);


        var x = await query.Select(ri => new
        {
            ri.Date.Date,
            ri.Route.RouteType.Name,
            ri.Route.RouteType.NameNL,
            Distance = (double)(((ri.Route.OverrideDistance.HasValue && ri.Route.OverrideDistance > 0) ? ri.Route.OverrideDistance : ri.Route.CalculatedDistance))
        }).ToListAsync();

        var x2 = x.GroupBy(x => x.Name).Select(x => (Name: x.Key, x.First().NameNL, Distance: Math.Round(x.Sum(x => x.Distance), 1))).OrderByDescending(x => x.Distance).ToList();
        var x4 = x.Where(ri => ri.Date.Month == DateTime.Now.Month).GroupBy(x => x.Name).Select(x => (Name: x.Key, x.First().NameNL, Distance: Math.Round(x.Sum(x => x.Distance), 1))).OrderByDescending(x => x.Distance).ToList();

        var collection = fontLoader.FontCollection;
        var fontType = collection.Get("Ubuntu");
        var font = new Font(fontType, 12);
        var smallfont = new Font(fontType, 10);
        var greenbrush = Brushes.Solid(Color.Green);
        var graybrush = Brushes.Solid(Color.Gray);
        var brush = Brushes.Solid(Color.Black);
        var spaceNeeded = TextMeasurer.MeasureAdvance("ovdb.infinityx.nl", new TextOptions(font)).Width;
        using var image = new Image<Rgba32>(width, height);
        image.Mutate(x =>
        {
            x.DrawText("ovdb.infinityx.nl", font, greenbrush, new PointF(width - spaceNeeded - 8, height - 20));
        });

        var postion = 0;
        var columnwidth = 10.0f;
        var distanceColumn = 10.0f;
        var distanceMonthColumn = 10.0f;

        if (!string.IsNullOrWhiteSpace(title))
        {
            image.Mutate(x =>
            {
                x.DrawText(title, new Font(font, FontStyle.Bold), brush, new PointF(0, postion));
            });
            postion += 20;
        }
        var total = x2.Sum(x => x.Distance);
        var monthTotal = x4.Sum(x => x.Distance);
        foreach (var method in x2)
        {

            var name = (!string.IsNullOrWhiteSpace(method.NameNL) && string.Equals(language, "NL", StringComparison.OrdinalIgnoreCase) ? method.NameNL : method.Name);
            var stringToDisplay = $"{name}: ";
            spaceNeeded = TextMeasurer.MeasureAdvance(stringToDisplay, new TextOptions(font)).Width;
            columnwidth = Math.Max(columnwidth, spaceNeeded + 8);
            distanceColumn = Math.Max(distanceColumn, TextMeasurer.MeasureAdvance($"{method.Distance:N0} km ", new TextOptions(font)).Width + 8);
        }
        foreach (var method in x4)
        {
            distanceMonthColumn = Math.Max(distanceMonthColumn, TextMeasurer.MeasureAdvance($"{method.Distance:N0} km ", new TextOptions(font)).Width);
        }
        var totalColumnWidth = columnwidth + distanceColumn + distanceMonthColumn + 8;

        var numberOfColumns = (int)(width / totalColumnWidth);

        if (includeTotal)
        {
            columnwidth = Math.Max(columnwidth, TextMeasurer.MeasureAdvance("Totaal: ", new TextOptions(font)).Width);
            distanceColumn = Math.Max(distanceColumn, TextMeasurer.MeasureAdvance($"{total:N0} km ", new TextOptions(font)).Width);
        }

        var maxItems = ((height - 20) / 20);
        if (!string.IsNullOrWhiteSpace(title))
            maxItems--;
        var column = 0;

        foreach (var method in x2.Take(maxItems * numberOfColumns))
        {
            var index = x2.IndexOf(method);
            if (index >= maxItems * (column + 1))
            {
                column++;
                postion = 0;
                if (!string.IsNullOrWhiteSpace(title))
                {
                    postion = 20;
                }
            }
            string name = !string.IsNullOrWhiteSpace(method.NameNL) && string.Equals(language, "NL", StringComparison.OrdinalIgnoreCase) ? method.NameNL : method.Name;
            string stringToDisplay = $"{name}: ";
            var month = x4.FindIndex(x => x.Name == method.Name);

            image.Mutate(x =>
            {
                x.DrawText(stringToDisplay, font, brush, new PointF(column * totalColumnWidth, postion));
            });

            spaceNeeded = TextMeasurer.MeasureAdvance($"{method.Distance:N0} km ", new TextOptions(font)).Width;
            image.Mutate(x =>
            {
                x.DrawText($"{method.Distance:N0} km ", font, brush, new PointF(columnwidth + (distanceColumn - spaceNeeded) + (column * totalColumnWidth), postion));
            });
            if (month >= 0)
            {
                string toShow = $"{x4[month].Distance:N0} km ";
                spaceNeeded = TextMeasurer.MeasureAdvance(toShow, new TextOptions(font)).Width;
                image.Mutate(x =>
                {
                    x.DrawText(toShow, font, brush, new PointF((columnwidth + distanceColumn + (distanceMonthColumn - spaceNeeded)) + 8 + (column * totalColumnWidth), postion));
                });
            }
            else
            {
                spaceNeeded = TextMeasurer.MeasureAdvance("0 km", new TextOptions(font)).Width;
                image.Mutate(x => { x.DrawText("0 km", font, brush, new PointF((columnwidth + distanceColumn + (distanceMonthColumn - spaceNeeded)) + 8 + (column * totalColumnWidth), postion)); });
            }
            postion += 20;
        }
        if (includeTotal)
        {
            image.Mutate(x =>
            {
                x.DrawLine(new DrawingOptions(), brush, 1, new PointF(1, height - 20), new PointF((int)(columnwidth + distanceColumn * 2), height - 20));
            });
            spaceNeeded = TextMeasurer.MeasureAdvance($"{total:N0} km", new TextOptions(font)).Width;
            image.Mutate(x =>
            {
                if (string.Equals(language, "NL", StringComparison.OrdinalIgnoreCase))
                {
                    x.DrawText($"Totaal: ", font, brush, new PointF(0, height - 20));
                }
                else
                {
                    x.DrawText($"Total: ", font, brush, new PointF(0, height - 20));
                }
            });
            image.Mutate(x =>
            {
                x.DrawText($"{total:N0} km", font, brush, new PointF(columnwidth + (distanceColumn - spaceNeeded), height - 20));
                x.DrawText($"{monthTotal:N0} km", font, brush, new PointF(columnwidth + distanceColumn + 8, height - 20));
            });
            postion += 20;
        }


        if (x2.Count > maxItems * numberOfColumns)
        {

            image.Mutate(x =>
            {
                x.DrawText("...", font, brush, new PointF(width - 20, height - 40));
            });
        }
        if (!includeTotal)
        {
            image.Mutate(x =>
            {
                x.DrawText(DateTime.Now.ToString("yyyy-MM-dd HH:mm"), smallfont, graybrush, new PointF(0, height - 16));
            });

        }
        using (MemoryStream ms = new MemoryStream())
        {
            image.SaveAsPng(ms);
            return ms.ToArray();
        }
    }
}
