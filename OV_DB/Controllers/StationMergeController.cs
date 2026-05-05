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

        // 500 m ≈ 0.0045° latitude; bounding-box pre-filter uses a slightly larger value
        private const double MaxDistanceMeters = 500.0;
        private const double BBoxDegrees = 0.007;

        /// <summary>
        /// Counts nearby pairs (within MaxDistanceMeters) that are not in the ignore set.
        /// The stations list must be pre-sorted by latitude.
        /// </summary>
        private static int CountNearbyPairs(
            IList<(int Id, double Lat, double Lon)> sortedStations,
            HashSet<(int, int)> ignoredSet)
        {
            int count = 0;
            for (int i = 0; i < sortedStations.Count; i++)
            {
                for (int j = i + 1; j < sortedStations.Count; j++)
                {
                    if (sortedStations[j].Lat - sortedStations[i].Lat > BBoxDegrees) break;
                    if (Math.Abs(sortedStations[i].Lon - sortedStations[j].Lon) > BBoxDegrees) continue;

                    var dist = HaversineDistance(
                        sortedStations[i].Lat, sortedStations[i].Lon,
                        sortedStations[j].Lat, sortedStations[j].Lon);

                    if (dist < MaxDistanceMeters)
                    {
                        var id1 = Math.Min(sortedStations[i].Id, sortedStations[j].Id);
                        var id2 = Math.Max(sortedStations[i].Id, sortedStations[j].Id);
                        if (!ignoredSet.Contains((id1, id2)))
                            count++;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Builds a hierarchical region name, e.g. "United Kingdom - England - Cornwall".
        /// </summary>
        private static string BuildRegionName(
            int regionId,
            Dictionary<int, (string Name, int? ParentId)> regionDict)
        {
            var parts = new List<string>();
            int? current = regionId;
            while (current.HasValue && regionDict.TryGetValue(current.Value, out var r))
            {
                parts.Add(r.Name);
                current = r.ParentId;
            }
            parts.Reverse();
            return string.Join(" - ", parts);
        }

        /// <summary>
        /// Returns all regions (at any level) that have non-hidden stations, with
        /// the number of unreviewed nearby pairs and a hierarchical name.
        /// </summary>
        [HttpGet("regions")]
        public async Task<IActionResult> GetRegions()
        {
            if (!IsAdmin()) return Forbid();

            // All non-hidden stations with each of their regions
            var stationsWithRegions = await DbContext.Stations
                .AsNoTracking()
                .Where(s => !s.Hidden)
                .SelectMany(s => s.Regions, (s, r) => new { s.Id, s.Lattitude, s.Longitude, RegionId = r.Id })
                .ToListAsync();

            if (!stationsWithRegions.Any())
                return Ok(new List<StationMergeCountryDTO>());

            // Group stations by region
            var stationsByRegion = stationsWithRegions
                .GroupBy(x => x.RegionId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => (x.Id, x.Lattitude, x.Longitude))
                          .Distinct()
                          .OrderBy(x => x.Lattitude)
                          .ToList());

            // Load all ignore pairs for these stations
            var allStationIds = stationsWithRegions.Select(x => x.Id).ToHashSet();
            var allIgnoredPairs = await DbContext.StationMergeIgnores
                .AsNoTracking()
                .Where(i => allStationIds.Contains(i.Station1Id))
                .Select(i => new { i.Station1Id, i.Station2Id })
                .ToListAsync();

            var ignoredSet = new HashSet<(int, int)>(
                allIgnoredPairs.Select(i => (i.Station1Id, i.Station2Id)));

            // Load all regions to build hierarchy
            var allRegions = await DbContext.Regions
                .AsNoTracking()
                .Select(r => new { r.Id, r.Name, r.ParentRegionId })
                .ToListAsync();

            var regionDict = allRegions.ToDictionary(
                r => r.Id,
                r => (r.Name, ParentId: r.ParentRegionId));

            var regionIds = stationsByRegion.Keys.ToHashSet();

            var result = regionIds
                .Select(rId =>
                {
                    var stations = stationsByRegion[rId];
                    var name = BuildRegionName(rId, regionDict);
                    return new StationMergeCountryDTO
                    {
                        RegionId = rId,
                        RegionName = name,
                        PairCount = CountNearbyPairs(stations, ignoredSet)
                    };
                })
                .Where(r => r.PairCount > 0 || stationsByRegion.ContainsKey(r.RegionId))
                .OrderBy(r => r.RegionName)
                .ToList();

            return Ok(result);
        }

        /// <summary>
        /// Returns paginated pairs of nearby stations (within 500 m) for a given region
        /// that have not yet been reviewed (not in the ignore list).
        /// </summary>
        [HttpGet("pairs/{regionId:int}")]
        public async Task<IActionResult> GetPairs(int regionId, [FromQuery] int page = 0, [FromQuery] int pageSize = 10)
        {
            if (!IsAdmin()) return Forbid();

            var stations = await DbContext.Stations
                .AsNoTracking()
                .Where(s => !s.Hidden && s.Regions.Any(r => r.Id == regionId))
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Lattitude,
                    s.Longitude,
                    s.Special,
                    Visits = s.StationVisits.Count()
                })
                .ToListAsync();

            var stationIds = stations.Select(s => s.Id).ToHashSet();

            var ignoredPairs = await DbContext.StationMergeIgnores
                .AsNoTracking()
                .Where(i => stationIds.Contains(i.Station1Id))
                .Select(i => new { i.Station1Id, i.Station2Id })
                .ToListAsync();

            var ignoredSet = new HashSet<(int, int)>(
                ignoredPairs.Select(i => (i.Station1Id, i.Station2Id)));

            stations.Sort((a, b) => a.Lattitude.CompareTo(b.Lattitude));

            var pairs = new List<StationNearbyPairDTO>();
            for (int i = 0; i < stations.Count; i++)
            {
                for (int j = i + 1; j < stations.Count; j++)
                {
                    if (stations[j].Lattitude - stations[i].Lattitude > BBoxDegrees) break;
                    if (Math.Abs(stations[i].Longitude - stations[j].Longitude) > BBoxDegrees) continue;

                    var dist = HaversineDistance(
                        stations[i].Lattitude, stations[i].Longitude,
                        stations[j].Lattitude, stations[j].Longitude);

                    if (dist >= MaxDistanceMeters) continue;

                    var id1 = Math.Min(stations[i].Id, stations[j].Id);
                    var id2 = Math.Max(stations[i].Id, stations[j].Id);

                    if (ignoredSet.Contains((id1, id2))) continue;

                    pairs.Add(new StationNearbyPairDTO
                    {
                        Station1Id = stations[i].Id,
                        Station1Name = stations[i].Name,
                        Station1Lattitude = stations[i].Lattitude,
                        Station1Longitude = stations[i].Longitude,
                        Station1Visits = stations[i].Visits,
                        Station1Special = stations[i].Special,
                        Station2Id = stations[j].Id,
                        Station2Name = stations[j].Name,
                        Station2Lattitude = stations[j].Lattitude,
                        Station2Longitude = stations[j].Longitude,
                        Station2Visits = stations[j].Visits,
                        Station2Special = stations[j].Special,
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

            var existingVisitorIds = await DbContext.StationVisits
                .Where(sv => sv.StationId == request.KeepStationId)
                .Select(sv => sv.UserId)
                .ToHashSetAsync();

            foreach (var visit in deleteStation.StationVisits)
            {
                if (!existingVisitorIds.Contains(visit.UserId))
                    visit.StationId = request.KeepStationId;
                else
                    DbContext.StationVisits.Remove(visit);
            }

            deleteStation.Hidden = true;

            var id1 = Math.Min(request.KeepStationId, request.DeleteStationId);
            var id2 = Math.Max(request.KeepStationId, request.DeleteStationId);
            var alreadyIgnored = await DbContext.StationMergeIgnores
                .AnyAsync(i => i.Station1Id == id1 && i.Station2Id == id2);
            if (!alreadyIgnored)
            {
                DbContext.StationMergeIgnores.Add(new StationMergeIgnore { Station1Id = id1, Station2Id = id2 });
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
                DbContext.StationMergeIgnores.Add(new StationMergeIgnore { Station1Id = id1, Station2Id = id2 });
                await DbContext.SaveChangesAsync();
            }

            return Ok(new { message = "Pair skipped." });
        }
    }
}
