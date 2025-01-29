using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using OV_DB.Services;
using OVDB_database.Database;
using Svg;
using Svg.Pathing;
using NetTopologySuite.IO;
using NetTopologySuite.IO.ShapeFile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.Claims;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MapImageController(OVDBDatabaseContext dbContext) : ControllerBase
    {
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetMap()
        {
            //var userIdClaim = int.Parse(User?.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            //if (userIdClaim < 0)
            //{
            //    return Forbid();
            //}
            var userIdClaim = 1;
            var regions = await dbContext.Regions.Where(r => !r.ParentRegionId.HasValue && r.LandMass != null)
                .Select(r => new
                {
                    r.Name,
                    r.LandMass,
                    Visited = r.Routes.Where(rout => rout.RouteMaps.Any(rm => rm.Map.UserId == userIdClaim) && rout.Regions.Any(rr => rr.Id == r.Id)).Any()
                })
                .ToListAsync();

            var maps = regions.Select(r => (r.Name, (Geometry)r.LandMass, r.Visited)).ToList();
            var generator = new CountrySvgGenerator(width: 800, height: 600, simplificationTolerance: 0.02d);

            string svgContent = generator.GenerateSvg(maps);
            byte[] bytes = Encoding.UTF8.GetBytes(svgContent);

            Response.Headers.Append("Content-Disposition", "inline");
            return File(bytes, "image/svg+xml");

        }


        [HttpGet("UPDATE")]
        [AllowAnonymous]
        public async Task<IActionResult> Update()
        {
            string shapefilePath = @"\\192.168.178.30\ovdb\shape\land_polygons.shp";
            using (var reader = new ShapefileDataReader(shapefilePath, NetTopologySuite.Geometries.GeometryFactory.Default))
            {
                object[] attributes = new object[100];
                // Iterate through each geometry in the shapefile
                while (reader.Read())
                {
                    var geometry = reader.Geometry;
                    reader.GetValues(attributes); // Get the attribute values for the current record

                    // Print out the geometry
                    Console.WriteLine($"Geometry: {geometry}");

                    // Iterate over the attributes and print their names and values
                    for (int i = 0; i < attributes.Length; i++)
                    {
                        var fieldName = reader.GetName(i); // Get the attribute name
                        var fieldValue = attributes[i];         // Get the attribute value
                        Console.WriteLine($"{fieldName}: {fieldValue}");
                    }

                    // You can handle specific geometry types here
                    if (geometry is Polygon polygon)
                    {
                        Console.WriteLine($"Polygon Area: {polygon.Area}");
                    }
                    else if (geometry is Point point)
                    {
                        Console.WriteLine($"Point: {point.X}, {point.Y}");
                    }
                }
            }

            return Ok();
        }
    }
}
