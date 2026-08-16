using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OV_DB.Services;
using OVDB_database.Enums;
using Microsoft.EntityFrameworkCore;
using OV_DB.Models;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
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
            var userIdClaim = User.GetUserId();
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
        public async Task<IActionResult> UpdateVisitedStations(int id, [FromBody] StationVisitUpdate value, [FromServices] IStationVisitService stationVisitService)
        {
            var userIdClaim = User.GetUserId();
            if (userIdClaim < 0)
            {
                return Forbid();
            }

            if (value.Visited)
            {
                // The web map sends no date: marking from the sofa says nothing about when, so the
                // visit joins the backfill queue undated rather than claiming today.
                await stationVisitService.MarkAsync(userIdClaim, id, value.Level, value.Date, StationVisitSource.Web);
            }
            else
            {
                await stationVisitService.UnmarkAsync(userIdClaim, id);
            }

            var visit = await stationVisitService.GetAsync(userIdClaim, id);
            var state = new StationVisitStateDTO
            {
                Visited = visit != null,
                // Every visit is at least a stop; an undated one is still stopped at, not unknown.
                Level = visit == null
                    ? null
                    : visit.FirstEntryExitDate.HasValue ? StationVisitLevel.EntryExit : StationVisitLevel.Stopped,
                FirstStoppedDate = visit?.FirstStoppedDate,
                FirstEntryExitDate = visit?.FirstEntryExitDate
            };

            var station = await DbContext.Stations.Include(s => s.Regions).SingleOrDefaultAsync(s => s.Id == id);
            if (station != null)
            {
                var regionIds = station.Regions.Select(r => r.Id).ToList();
                var totalStationsInRegion = await DbContext.Stations.CountAsync(s => s.Regions.Any(r => regionIds.Contains(r.Id)));
                if (totalStationsInRegion > 0)
                {
                    var visitedStationsInRegion = await DbContext.StationVisits.CountAsync(sv => sv.UserId == userIdClaim && sv.Station.Regions.Any(r => regionIds.Contains(r.Id)));
                    state.PercentageVisited = (double)visitedStationsInRegion / totalStationsInRegion * 100;
                }
            }

            return Ok(state);
        }

        [HttpGet("map")]
        public async Task<IActionResult> GetAdminMap([FromQuery] List<int> regions)
        {
            var adminClaim = (User.IsAdmin() ? "true" : "false");
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
            var adminClaim = (User.IsAdmin() ? "true" : "false");
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
            var adminClaim = (User.IsAdmin() ? "true" : "false");
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
