using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
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
        private readonly IMapper _mapper;

        public StationMapsController(OVDBDatabaseContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<StationMapDTO>>> GetMaps()
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            return await _context.StationGroupings.Where(m => m.UserId == userIdClaim)
                .OrderBy(m => m.OrderNr)
                .ProjectTo<StationMapDTO>(_mapper.ConfigurationProvider)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<StationMapDTO>> GetMap(int id)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var map = await _context.StationGroupings.Where(m => m.UserId == userIdClaim)
                .ProjectTo<StationMapDTO>(_mapper.ConfigurationProvider)
                .SingleOrDefaultAsync(m => m.Id == id);

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
            var dbStationMap = await _context.StationGroupings.Where(m => m.UserId == userIdClaim).Include(s => s.Regions)
                .SingleOrDefaultAsync(m => m.Id == stationMap.Id);
            if (dbStationMap is null)
            {
                return NotFound();
            }
            dbStationMap.SharingLinkName = stationMap.SharingLinkName;
            dbStationMap.Name = stationMap.Name;
            dbStationMap.NameNL = stationMap.NameNL;

            var regions = await _context.Regions.Where(r => stationMap.RegionIds.Contains(r.Id)).ToListAsync();
            dbStationMap.Regions = regions;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MapExists(stationMap.Id))
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

            var dbStationMap = new StationGrouping
            {
                SharingLinkName = stationMap.SharingLinkName,
                Name = stationMap.Name,
                NameNL = stationMap.NameNL,
                UserId = userIdClaim,
                MapGuid = Guid.NewGuid()
            };

            var regions = await _context.Regions.Where(r => stationMap.RegionIds.Contains(r.Id)).ToListAsync();
            dbStationMap.Regions = regions;

            _context.StationGroupings.Add(dbStationMap);
            await _context.SaveChangesAsync();

            return Ok(dbStationMap);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<StationGrouping>> DeleteMap(int id)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var stationMap = await _context.StationGroupings.Where(m => m.UserId == userIdClaim).SingleOrDefaultAsync(m => m.Id == id);
            if (stationMap == null)
            {
                return NotFound();
            }
            _context.StationGroupings.Remove(stationMap);
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
            var maps = await _context.StationGroupings.Where(rt => rt.UserId == userIdClaim).ToListAsync();

            maps.ForEach(r => r.OrderNr = mapOrdering.FindIndex(i => r.Id == i));
            _context.StationGroupings.UpdateRange(maps);
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
            var stationMap = await _context.StationGroupings.Include(r => r.Regions).SingleOrDefaultAsync(sm => sm.MapGuid == guid);
            if (stationMap is null)
            {
                return NotFound();
            }
            var regionIds = stationMap.Regions.Select(r => r.Id).ToList();
            var stationsQuery = _context.Stations.AsQueryable();
            stationsQuery = stationsQuery.Where(s => s.Regions.Any(r => regionIds.Contains(r.Id)))
                .Where(s => s.Hidden == false)
                .Where(s => s.Special == false);

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



            var response = new StationView
            {
                Stations = stations,
                Total = stations.Count,
                Name = stationMap.Name,
                NameNL = stationMap.NameNL,
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
