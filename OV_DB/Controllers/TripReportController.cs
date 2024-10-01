using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OV_DB.Models;
using OVDB_database.Database;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TripReportController : ControllerBase
    {
        private readonly OVDBDatabaseContext _databaseContext;

        public TripReportController(OVDBDatabaseContext databaseContext)
        {
            _databaseContext = databaseContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetTripReport([FromQuery] List<Guid> guid, [FromQuery] int year, [FromQuery] bool english = false)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var workbook = new XLWorkbook();
            var ws = workbook.AddWorksheet("trips");
            var row = 1;
            var routes = await _databaseContext.RouteInstances
                .Where(ri => ri.Date.Year == year &&
                (
                    ri.Route.RouteMaps.Any(rm => rm.Map.UserId == userIdClaim && guid.Contains(rm.Map.MapGuid)) ||
                    ri.RouteInstanceMaps.Any(rm => rm.Map.UserId == userIdClaim && guid.Contains(rm.Map.MapGuid)))
                )
                .Select(ri => new TripReportEntity
                {
                    Date = ri.Date,
                    Description = ri.Route.Description,
                    DescriptionNL = ri.Route.DescriptionNL,
                    ExtraInfo = ri.RouteInstanceProperties,
                    Line = ri.Route.LineNumber,
                    Operator = ri.Route.OperatingCompany,
                    Name = ri.Route.Name,
                    NameNL = ri.Route.NameNL,
                    From = ri.Route.From,
                    To = ri.Route.To,
                    Type = ri.Route.RouteType.Name,
                    TypeNL = ri.Route.RouteType.NameNL,
                    Distance = ri.Route.OverrideDistance.HasValue ? ri.Route.OverrideDistance.Value : ri.Route.CalculatedDistance
                })
                .OrderBy(ri => ri.Date).ThenBy(ri => ri.Line).ToListAsync();

            var extraInfo = routes.SelectMany(ri => ri.ExtraInfo.Select(ri => ri.Key)).Distinct().OrderBy(d => d).ToList();

            if (!english)
            {
                ws.Cell(row, 1).Value = "Datum";
                ws.Cell(row, 2).Value = "Type";
                ws.Cell(row, 3).Value = "Bedienend bedrijf";
                ws.Cell(row, 4).Value = "Lijn";
                ws.Cell(row, 5).Value = "Van";
                ws.Cell(row, 6).Value = "Tot";
                ws.Cell(row, 7).Value = "Afstand (km)";
                ws.Cell(row, 8).Value = "Naam";
                ws.Cell(row, 9).Value = "Opmerking";
            }
            else
            {
                ws.Cell(row, 1).Value = "Date";
                ws.Cell(row, 2).Value = "Type";
                ws.Cell(row, 3).Value = "Operating company";
                ws.Cell(row, 4).Value = "Line";
                ws.Cell(row, 5).Value = "From";
                ws.Cell(row, 6).Value = "To";
                ws.Cell(row, 7).Value = "Distance (km)";
                ws.Cell(row, 8).Value = "Name";
                ws.Cell(row, 9).Value = "Remarks";
            }
            var column = 10;
            extraInfo.ForEach(key =>
            {
                ws.Cell(row, column).Value = key;
                column++;
            });

            row++;
            routes.ForEach(route =>
            {
                if (string.IsNullOrWhiteSpace(route.From) && string.IsNullOrWhiteSpace(route.To))
                {
                    if (route.Name.Contains(':') && route.Name.Contains("=>"))
                    {
                        var trip = route.Name.Split(':')[1];
                        var parts = trip.Split("=>");
                        route.From = parts[0].Trim();
                        route.To = parts[1].Trim();
                    }
                }

                ws.Cell(row, 1).Value = route.Date.Date;
                ws.Cell(row, 2).Value = (!english && !string.IsNullOrWhiteSpace(route.TypeNL)) ? route.TypeNL : route.Type;
                ws.Cell(row, 3).Value = route.Operator;
                ws.Cell(row, 4).Value = route.Line;
                ws.Cell(row, 5).Value = route.From;
                ws.Cell(row, 6).Value = route.To;
                ws.Cell(row, 7).Value = Math.Round(route.Distance, 2);
                ws.Cell(row, 8).Value = (!english && !string.IsNullOrWhiteSpace(route.NameNL)) ? route.NameNL : route.Name;
                ws.Cell(row, 9).Value = (!english && !string.IsNullOrWhiteSpace(route.DescriptionNL)) ? route.DescriptionNL : route.Description;
                route.ExtraInfo.ForEach(ri =>
                {
                    var key = ri.Key;
                    var index = 10 + extraInfo.IndexOf(key);
                    if (ri.Bool != null)
                    {
                        if (english)
                            ws.Cell(row, index).Value = ri.Bool.Value ? "true" : "false";
                        else
                            ws.Cell(row, index).Value = ri.Bool.Value ? "waar" : "onwaar";
                    }
                    else
                    {
                        ws.Cell(row, index).Value = ri.Value;
                    }
                });
                row++;
            });

            using (var stream = new MemoryStream())
            {
                workbook.SaveAs(stream);
                stream.Flush();

                return new FileContentResult(stream.ToArray(),
                       "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
                {
                    FileDownloadName = "tripreport.xlsx"
                };
            }
        }
    }
}
