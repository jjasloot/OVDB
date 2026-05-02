using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OV_DB.Models;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class StationMergeController : ControllerBase
    {
        private OVDBDatabaseContext DbContext { get; }

        public StationMergeController(OVDBDatabaseContext dbContext)
        {
            DbContext = dbContext;
        }

        private bool IsAdmin() =>
            string.Equals(
                User.Claims.SingleOrDefault(c => c.Type == "admin")?.Value ?? "false",
                "true",
                StringComparison.OrdinalIgnoreCase);

        private static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000.0;
            var dLat = (lat2 - lat1) * Math.PI / 180.0;
            var dLon = (lon2 - lon1) * Math.PI / 180.0;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                  + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
                  * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        /// <summary>
        /// Returns all station countries that have at least two non-hidden stations, to drive the country selector.
        /// </summary>
        [HttpGet("countries")]
        public async Task<IActionResult> GetCountries()
        {
            if (!IsAdmin()) return Forbid();

            var countries = await DbContext.StationCountries
                .Where(c => c.Stations.Any(s => !s.Hidden))
                .OrderBy(c => c.Name)
                .Select(c => new StationMergeCountryDTO
                {
                    CountryId = c.Id,
                    CountryName = c.Name
                })
                .ToListAsync();

            return Ok(countries);
        }

        /// <summary>
        /// Returns paginated pairs of nearby stations (within 200 m) for a given country
        /// that have not yet been reviewed (not in the ignore list).
        /// </summary>
        [HttpGet("pairs/{countryId:int}")]
        public async Task<IActionResult> GetPairs(int countryId, [FromQuery] int page = 0, [FromQuery] int pageSize = 10)
        {
            if (!IsAdmin()) return Forbid();

            const double MaxDistanceMeters = 200.0;
            // 200 m ≈ 0.0018° latitude; use a slightly larger bounding box as pre-filter
            const double BBoxDegrees = 0.003;

            var stations = await DbContext.Stations
                .AsNoTracking()
                .Where(s => s.StationCountryId == countryId && !s.Hidden)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Lattitude,
                    s.Longitude,
                    Visits = s.StationVisits.Count()
                })
                .ToListAsync();

            var ignoredPairs = await DbContext.StationMergeIgnores
                .AsNoTracking()
                .Where(i => DbContext.Stations
                    .Where(s => s.StationCountryId == countryId)
                    .Select(s => s.Id)
                    .Contains(i.Station1Id))
                .Select(i => new { i.Station1Id, i.Station2Id })
                .ToListAsync();

            var ignoredSet = new HashSet<(int, int)>(
                ignoredPairs.Select(i => (Math.Min(i.Station1Id, i.Station2Id), Math.Max(i.Station1Id, i.Station2Id))));

            // Sort by latitude to allow early exit in inner loop
            stations.Sort((a, b) => a.Lattitude.CompareTo(b.Lattitude));

            var pairs = new List<StationNearbyPairDTO>();
            for (int i = 0; i < stations.Count; i++)
            {
                for (int j = i + 1; j < stations.Count; j++)
                {
                    // Early exit: if latitude difference exceeds bounding box, no more candidates
                    if (stations[j].Lattitude - stations[i].Lattitude > BBoxDegrees)
                        break;

                    if (Math.Abs(stations[i].Longitude - stations[j].Longitude) > BBoxDegrees)
                        continue;

                    var dist = HaversineDistance(
                        stations[i].Lattitude, stations[i].Longitude,
                        stations[j].Lattitude, stations[j].Longitude);

                    if (dist >= MaxDistanceMeters)
                        continue;

                    var id1 = Math.Min(stations[i].Id, stations[j].Id);
                    var id2 = Math.Max(stations[i].Id, stations[j].Id);

                    if (ignoredSet.Contains((id1, id2)))
                        continue;

                    pairs.Add(new StationNearbyPairDTO
                    {
                        Station1Id = stations[i].Id,
                        Station1Name = stations[i].Name,
                        Station1Lattitude = stations[i].Lattitude,
                        Station1Longitude = stations[i].Longitude,
                        Station1Visits = stations[i].Visits,
                        Station2Id = stations[j].Id,
                        Station2Name = stations[j].Name,
                        Station2Lattitude = stations[j].Lattitude,
                        Station2Longitude = stations[j].Longitude,
                        Station2Visits = stations[j].Visits,
                        DistanceMeters = Math.Round(dist, 1)
                    });
                }
            }

            var total = pairs.Count;
            var paged = pairs.Skip(page * pageSize).Take(pageSize).ToList();

            return Ok(new { total, pairs = paged });
        }

        /// <summary>
        /// Merges two stations: reassigns all StationVisits from the deleted station to the kept station
        /// (skipping duplicates), then hides the deleted station.
        /// </summary>
        [HttpPost("merge")]
        public async Task<IActionResult> MergeStations([FromBody] StationMergeRequestDTO request)
        {
            if (!IsAdmin()) return Forbid();

            if (request.KeepStationId == request.DeleteStationId)
                return BadRequest("Keep and delete station IDs must be different.");

            var keepStation = await DbContext.Stations.SingleOrDefaultAsync(s => s.Id == request.KeepStationId);
            var deleteStation = await DbContext.Stations
                .Include(s => s.StationVisits)
                .SingleOrDefaultAsync(s => s.Id == request.DeleteStationId);

            if (keepStation == null || deleteStation == null)
                return NotFound();

            // Reassign StationVisits that don't already exist on the keep station
            var existingVisitorIds = await DbContext.StationVisits
                .Where(sv => sv.StationId == request.KeepStationId)
                .Select(sv => sv.UserId)
                .ToHashSetAsync();

            foreach (var visit in deleteStation.StationVisits)
            {
                if (!existingVisitorIds.Contains(visit.UserId))
                {
                    visit.StationId = request.KeepStationId;
                }
                else
                {
                    DbContext.StationVisits.Remove(visit);
                }
            }

            // Hide the deleted station
            deleteStation.Hidden = true;

            // Record the pair as reviewed so it won't show up again
            var id1 = Math.Min(request.KeepStationId, request.DeleteStationId);
            var id2 = Math.Max(request.KeepStationId, request.DeleteStationId);
            var alreadyIgnored = await DbContext.StationMergeIgnores
                .AnyAsync(i => i.Station1Id == id1 && i.Station2Id == id2);
            if (!alreadyIgnored)
            {
                DbContext.StationMergeIgnores.Add(new StationMergeIgnore
                {
                    Station1Id = id1,
                    Station2Id = id2
                });
            }

            await DbContext.SaveChangesAsync();

            return Ok(new { message = "Stations merged successfully." });
        }

        /// <summary>
        /// Marks a pair as "keep both" – the pair will no longer appear in the merge queue.
        /// </summary>
        [HttpPost("skip")]
        public async Task<IActionResult> SkipPair([FromBody] StationMergeSkipDTO request)
        {
            if (!IsAdmin()) return Forbid();

            var id1 = Math.Min(request.Station1Id, request.Station2Id);
            var id2 = Math.Max(request.Station1Id, request.Station2Id);

            var alreadyIgnored = await DbContext.StationMergeIgnores
                .AnyAsync(i => i.Station1Id == id1 && i.Station2Id == id2);

            if (!alreadyIgnored)
            {
                DbContext.StationMergeIgnores.Add(new StationMergeIgnore
                {
                    Station1Id = id1,
                    Station2Id = id2
                });
                await DbContext.SaveChangesAsync();
            }

            return Ok(new { message = "Pair skipped." });
        }
    }
}
