using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OVDB_database.Database;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImagesController : ControllerBase
    {
        private readonly OVDBDatabaseContext _context;
        private readonly IMemoryCache _memoryCache;

        public ImagesController(OVDBDatabaseContext context, IMemoryCache memoryCache)
        {
            _context = context;
            _memoryCache = memoryCache;
        }


        [HttpGet("{guid:Guid}")]
        public async Task<ActionResult> GetImageAsync(Guid guid, [FromQuery] int width = 300, [FromQuery] int height = 100, [FromQuery] string title = null, [FromQuery] bool includeTotal = false)
        {
            var id = "image|" + guid.ToString() + "|" + width + "|" + height + "|" + includeTotal + "|" + title;

            var fileContents = await _memoryCache.GetOrCreateAsync(id, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
                return await GenerateImageAsync(width, height, title, guid, includeTotal);
            });
            //var fileContents = await GenerateImageAsync(width, height, title, guid, includeTotal);
            return File(fileContents, "image/png");
        }

        private async Task<byte[]> GenerateImageAsync(int width, int height, string title, Guid guid, bool includeTotal)
        {
            var query = _context.RouteInstances
     .Where(ri => ri.Route.RouteMaps.Any(rm => rm.Map.MapGuid == guid));

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



            using var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            using var brush = new SolidBrush(Color.Black);
            using var font = new Font("Ubuntu", 12);
            using var smallfont = new Font("Ubuntu", 10);
            using var greenbrush = new SolidBrush(Color.Green);
            using var graybrush = new SolidBrush(Color.Gray);
            var spaceNeeded = graphics.MeasureString("ovdb.infinityx.nl", font).Width;
            graphics.DrawString("ovdb.infinityx.nl", font, greenbrush, new PointF(width - spaceNeeded, height - 20));

            var postion = 0;
            var columnwidth = 10.0f;
            var distanceColumn = 10.0f;
            var distanceMonthColumn = 10.0f;

            if (!string.IsNullOrWhiteSpace(title))
            {
                graphics.DrawString(title, new Font(font, FontStyle.Bold), brush, new PointF(0, postion));
                postion += 20;
            }
            var total = x2.Sum(x => x.Distance);
            var monthTotal = x4.Sum(x => x.Distance);
            foreach (var method in x2)
            {

                string name = (!string.IsNullOrWhiteSpace(method.NameNL) ? method.NameNL : method.Name);
                string stringToDisplay = $"{name}: ";
                spaceNeeded = graphics.MeasureString(stringToDisplay, font).Width;
                columnwidth = Math.Max(columnwidth, spaceNeeded);
                distanceColumn = Math.Max(distanceColumn, graphics.MeasureString($"{method.Distance:N0} km ", font).Width);
            }
            foreach (var method in x4)
            {
                distanceMonthColumn = Math.Max(distanceMonthColumn, graphics.MeasureString($"{method.Distance:N0} km ", font).Width);
            }
            var totalColumnWidth = columnwidth + distanceColumn + distanceMonthColumn + 8;

            var numberOfColumns = (int)(width / totalColumnWidth);

            if (includeTotal)
            {
                columnwidth = Math.Max(columnwidth, graphics.MeasureString("Totaal: ", font).Width);
                distanceColumn = Math.Max(distanceColumn, graphics.MeasureString($"{total:N0} km ", font).Width);
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
                string name = !string.IsNullOrWhiteSpace(method.NameNL) ? method.NameNL : method.Name;
                string stringToDisplay = $"{name}: ";
                var month = x4.FindIndex(x => x.Name == method.Name);


                graphics.DrawString(stringToDisplay, font, brush, new PointF(column * totalColumnWidth, postion));

                spaceNeeded = graphics.MeasureString($"{method.Distance:N0} km", font).Width;
                graphics.DrawString($"{method.Distance:N0} km", font, brush, new PointF((columnwidth + (distanceColumn - spaceNeeded)) + (column * totalColumnWidth), postion));
                if (month >= 0)
                {
                    string toShow = $"{x4[month].Distance:N0} km";
                    spaceNeeded = graphics.MeasureString(toShow, font).Width;

                    graphics.DrawString(toShow, font, brush, new PointF((columnwidth + distanceColumn + (distanceMonthColumn - spaceNeeded)) + (column * totalColumnWidth), postion));
                }
                else
                {
                    spaceNeeded = graphics.MeasureString("0 km", font).Width;
                    graphics.DrawString("0 km", font, brush, new PointF((columnwidth + distanceColumn + (distanceMonthColumn - spaceNeeded)) + (column * totalColumnWidth), postion));
                }
                postion += 20;
            }
            if (includeTotal)
            {
                using var pen = new Pen(Color.Black);
                graphics.DrawLine(pen, new Point(1, height - 20), new Point((int)(columnwidth + distanceColumn * 2), height - 20));
                spaceNeeded = graphics.MeasureString($"{total:N0} km", font).Width;
                graphics.DrawString($"Totaal: ", font, brush, new PointF(0, height - 20));
                graphics.DrawString($"{total:N0} km", font, brush, new PointF(columnwidth + (distanceColumn - spaceNeeded), height - 20));
                graphics.DrawString($"{monthTotal:N0} km", font, brush, new PointF(columnwidth + distanceColumn, height - 20));
                postion += 20;
            }


            if (x2.Count > maxItems * numberOfColumns)
            {
                graphics.DrawString("...", font, brush, new PointF(width - 20, height - 40));

            }
            if (!includeTotal)
                graphics.DrawString(DateTime.Now.ToString("yyyy-MM-dd HH:mm"), smallfont, graybrush, new PointF(0, height - 16));
            using (MemoryStream ms = new MemoryStream())
            {
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return ms.ToArray();
            }
        }
    }
}
