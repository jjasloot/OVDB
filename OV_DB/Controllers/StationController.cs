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
            var stationsQuery = DbContext.Stations.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(countryIds))
            {
                var countries = countryIds.Split(',').Select(s => int.Parse(s)).ToList();
                stationsQuery = stationsQuery.Where(s => s.StationCountryId.HasValue).Where(s => countries.Contains(s.StationCountryId.Value));
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

            var station = await DbContext.Stations.Include(s => s.Regions).SingleOrDefaultAsync(s => s.Id == id);
            if (station != null)
            {
                var regionIds = station.Regions.Select(r => r.Id).ToList();
                var totalStationsInRegion = await DbContext.Stations.CountAsync(s => s.Regions.Any(r => regionIds.Contains(r.Id)));
                var visitedStationsInRegion = await DbContext.StationVisits.CountAsync(sv => sv.UserId == userIdClaim && sv.Station.Regions.Any(r => regionIds.Contains(r.Id)));
                var percentageVisited = (double)visitedStationsInRegion / totalStationsInRegion * 100;

                return Ok(new { Message = "Station visit status updated.", PercentageVisited = percentageVisited });
            }

            return Ok(new { Message = "Station visit status updated." });
        }

        [HttpGet("map")]
        public async Task<IActionResult> GetAdminMap([FromQuery] List<int> regions)
        {
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var stations = DbContext.Stations.AsNoTracking().AsQueryable();

            if (regions != null && regions.Any())
            {
                stations = stations.Where(s => s.Regions.Any(r => regions.Contains(r.Id)));
            }
            var stationsQuery = stations.Select(s => new StationAdminPropertiesDTO
            {
                Name = s.Name,
                Hidden = s.Hidden,
                Special = s.Special,
                Id = s.Id,
                Lattitude = s.Lattitude,
                Longitude = s.Longitude,
                StationVisits = s.StationVisits.Count()
            });
            return Ok(await stationsQuery.ToListAsync());
        }

        [HttpPut("admin/{id:int}")]
        public async Task<IActionResult> AdminUpdateStation(int id, [FromBody] StationVisibilityAdmin stationVisibility)
        {
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var station = await DbContext.Stations.SingleOrDefaultAsync(s => s.Id == id);
            if (station == null)
            {
                return NotFound();
            }

            station.Hidden = stationVisibility.Hidden;
            station.Special = stationVisibility.Special;

            await DbContext.SaveChangesAsync();

            return Ok();
        }

        [HttpDelete("admin/{id:int}")]
        public async Task<IActionResult> AdminDeleteStation(int id)
        {
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var station = await DbContext.Stations.SingleOrDefaultAsync(s => s.Id == id);
            if (station == null)
            {
                return NotFound();
            }

            station.Hidden = true;
            await DbContext.SaveChangesAsync();

            return Ok();
        }
    }
}
