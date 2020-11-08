using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OV_DB.Models;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StationController : ControllerBase
    {
        private OVDBDatabaseContext DbContext { get; }
        public StationController(OVDBDatabaseContext dbContext)
        {
            DbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetVisitedStations([FromQuery] string countryIds = "")
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var stationsQuery = DbContext.Stations.AsQueryable();
            if (!string.IsNullOrWhiteSpace(countryIds))
            {
                var countries = countryIds.Split(',').Select(s => int.Parse(s)).ToList();
                stationsQuery = stationsQuery.Where(s => countries.Contains(s.StationCountryId));
            }

            var stations = await stationsQuery.Select(s => new StationDTO
            {
                Elevation = s.Elevation,
                Id = s.Id,
                Lattitude = s.Lattitude,
                Longitude = s.Longitude,
                Name = s.Name,
                Network = s.Network,
                Operator = s.Operator,
                Visited = s.StationVisits.Any(sv => sv.UserId == userIdClaim)
            }).ToListAsync();

            var collection = new FeatureCollection();
            stations.ForEach(s =>
            {
                var properties = new StationPropertiesDTO();
                if (!string.IsNullOrWhiteSpace(s.Name))
                    properties.name = s.Name;
                if (!string.IsNullOrWhiteSpace(s.Network))
                    properties.network = s.Network;
                if (!string.IsNullOrWhiteSpace(s.Operator))
                    properties.operatingCompany = s.Operator;
                if (s.Elevation.HasValue)
                    properties.elevation = s.Elevation.Value;
                properties.visited = s.Visited;
                properties.id = s.Id;
                Position coordinates = new Position(s.Lattitude, s.Longitude, s.Elevation);
                Point geometry = new Point(coordinates);
                var item = new Feature(geometry, properties, null);

                collection.Features.Add(item);
            });


            return Ok(collection);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateVisitedStations(int id, [FromBody] BoolValue value)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }

            var stationVisit = await DbContext.StationVisits.Where(sv => sv.StationId == id && sv.UserId == userIdClaim).SingleOrDefaultAsync();

            if (value.Value)
            {
                if (stationVisit == null)
                {
                    DbContext.Add(new StationVisit { StationId = id, UserId = userIdClaim });
                }
            }
            else
            {
                if (stationVisit != null)
                {
                    DbContext.Remove(stationVisit);
                }
            }
            await DbContext.SaveChangesAsync();
            return Ok();
        }
    }
}
