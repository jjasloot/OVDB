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
    public class StationMapsController : ControllerBase
    {
        private OVDBDatabaseContext _context;

        public StationMapsController(OVDBDatabaseContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<StationMap>>> GetMaps()
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            return await _context.StationMaps.Where(m => m.UserId == userIdClaim).OrderBy(m => m.OrderNr).ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<StationMap>> GetMap(int id)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var map = await _context.StationMaps.Where(m => m.UserId == userIdClaim).SingleOrDefaultAsync(m => m.StationMapId == id);

            if (map == null)
            {
                return NotFound();
            }

            return map;
        }

        [HttpPut]
        public async Task<IActionResult> PutMap(StationMapDTO stationMap)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var dbStationMap = await _context.StationMaps.Where(m => m.UserId == userIdClaim).SingleOrDefaultAsync(m => m.StationMapId == stationMap.StationMapId);

            dbStationMap.SharingLinkName = stationMap.SharingLinkName;
            dbStationMap.Name = stationMap.Name;
            dbStationMap.NameNL = stationMap.NameNL;

            var countries = await _context.StationCountries.Where(sc => stationMap.StationMapCountries.Any(smc => smc.StationCountryId == sc.Id)).ToListAsync();

            dbStationMap.StationMapCountries = stationMap.StationMapCountries.Select(c =>
            new StationMapCountry
            {
                StationCountryId = c.StationCountryId,
                IncludeSpecials = c.IncludeSpecials
            }).ToList();

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MapExists(stationMap.StationMapId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        [HttpPost]
        public async Task<ActionResult<Map>> PostMap(StationMapDTO stationMap)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }

            var dbStationMap = new StationMap
            {
                SharingLinkName = stationMap.SharingLinkName,
                Name = stationMap.Name,
                NameNL = stationMap.NameNL,
                UserId = userIdClaim,
                MapGuid = Guid.NewGuid()
            };
            var countries = await _context.StationCountries.Where(sc => stationMap.StationMapCountries.Any(smc => smc.StationCountryId == sc.Id)).ToListAsync();

            dbStationMap.StationMapCountries = stationMap.StationMapCountries.Select(c =>
            new StationMapCountry
            {
                StationCountryId = c.StationCountryId,
                IncludeSpecials = c.IncludeSpecials
            }).ToList();

            _context.StationMaps.Add(dbStationMap);
            await _context.SaveChangesAsync();

            return CreatedAtAction("StationMap", new { id = dbStationMap.StationMapId }, dbStationMap);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<StationMap>> DeleteMap(int id)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var stationMap = await _context.StationMaps.Where(m => m.UserId == userIdClaim).SingleOrDefaultAsync(m => m.StationMapId == id);
            if (stationMap == null)
            {
                return NotFound();
            }
            var routeMaps = await _context.StationMapCountries.Where(rm => rm.StationMapId == stationMap.StationMapId).ToListAsync();
            _context.StationMapCountries.RemoveRange(routeMaps);
            _context.StationMaps.Remove(stationMap);
            await _context.SaveChangesAsync();

            return stationMap;
        }

        [HttpPost("order")]
        public async Task<ActionResult> UpdateMapsOrdering([FromBody] List<int> mapOrdering)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var maps = await _context.StationMaps.Where(rt => rt.UserId == userIdClaim).ToListAsync();

            maps.ForEach(r => r.OrderNr = mapOrdering.FindIndex(i => r.StationMapId == i));
            _context.StationMaps.UpdateRange(maps);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("map/{id}")]
        public async Task<IActionResult> GetVisitedStations(string id)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var guid = Guid.Parse(id);

            var stationsQuery = _context.Stations.AsQueryable();
            stationsQuery = stationsQuery.Where(s => s.StationCountry.StationMapCountries.Any(smc => smc.StationMap.MapGuid == guid))
                .Where(s => s.Hidden == false)
                .Where(s => s.Special == false || s.StationCountry.StationMapCountries.Any(smc => smc.StationMap.MapGuid == guid && smc.IncludeSpecials == true));

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
                var coordinates = new Position(s.Lattitude, s.Longitude, s.Elevation);
                var geometry = new Point(coordinates);
                var item = new Feature(geometry, properties, null);

                collection.Features.Add(item);
            });

            var response = new StationView
            {
                GeoJson = collection,
                Total = stations.Count,
                Visited = stations.Where(s => s.Visited).Count()
            };
            return Ok(response);
        }

        [HttpGet("countries")]
        public async Task<IActionResult> GetCountries()
        {
            var countries = await _context.StationCountries.ToListAsync();
            return Ok(countries);
        }

        private bool MapExists(int id)
        {
            return _context.Maps.Any(e => e.MapId == id);
        }
    }
}
