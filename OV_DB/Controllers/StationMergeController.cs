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

        private const double MaxDistanceMeters = 500.0;
        /// <summary>Conservative latitude bounding-box pre-filter (~500 m).</summary>
        private const double LatBBoxDegrees = 0.005;
        private const double EarthRadius = 6371000.0;
        /// <summary>Maximum pair count returned or counted; prevents runaway iteration on dense regions.</summary>
        private const int MaxPairCount = 9999;

        /// <summary>
        /// Computes a conservative longitude bounding-box for the given latitude.
        /// At higher latitudes, 1° of longitude covers fewer metres, so the pre-filter
        /// degree threshold must be widened to avoid false negatives.
        /// </summary>
        private static double LonBBoxDegrees(double latDeg)
        {
            var cosLat = Math.Cos(latDeg * Math.PI / 180.0);
            if (cosLat < 0.001) return 180.0; // near pole – no lon restriction
            return MaxDistanceMeters / (EarthRadius * cosLat * Math.PI / 180.0);
        }

        private static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var dLat = (lat2 - lat1) * Math.PI / 180.0;
            var dLon = (lon2 - lon1) * Math.PI / 180.0;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                  + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
                  * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return EarthRadius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        /// <summary>
        /// Counts nearby pairs (within MaxDistanceMeters, inclusive) that are not in the ignore set.
        /// The stations list must be pre-sorted by latitude.
        /// Uses a per-station longitude bounding box to avoid false negatives at high latitudes.
        /// </summary>
        private static int CountNearbyPairs(
            IList<(int Id, double Lat, double Lon)> sortedStations,
            HashSet<(int, int)> ignoredSet)
        {
            int count = 0;
            for (int i = 0; i < sortedStations.Count; i++)
            {
                if (count >= MaxPairCount) break;
                var latI = sortedStations[i].Lat;
                var lonBBox = LonBBoxDegrees(latI);
                for (int j = i + 1; j < sortedStations.Count; j++)
                {
                    if (sortedStations[j].Lat - latI > LatBBoxDegrees) break;
                    if (Math.Abs(sortedStations[i].Lon - sortedStations[j].Lon) > lonBBox) continue;

                    var dist = HaversineDistance(
                        latI, sortedStations[i].Lon,
                        sortedStations[j].Lat, sortedStations[j].Lon);

                    if (dist <= MaxDistanceMeters)
                    {
                        var id1 = Math.Min(sortedStations[i].Id, sortedStations[j].Id);
                        var id2 = Math.Max(sortedStations[i].Id, sortedStations[j].Id);
                        if (!ignoredSet.Contains((id1, id2)))
                        {
                            count++;
                            if (count >= MaxPairCount) break;
                        }
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

            var stationsWithRegions = await DbContext.Stations
                .AsNoTracking()
                .Where(s => !s.Hidden)
                .SelectMany(s => s.Regions, (s, r) => new { s.Id, s.Lattitude, s.Longitude, RegionId = r.Id })
                .ToListAsync();

            if (!stationsWithRegions.Any())
                return Ok(new List<StationMergeCountryDTO>());

            var stationsByRegion = stationsWithRegions
                .GroupBy(x => x.RegionId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => (x.Id, x.Lattitude, x.Longitude))
                          .DistinctBy(x => x.Id)
                          .OrderBy(x => x.Lattitude)
                          .ToList());

            var allStationIds = stationsWithRegions.Select(x => x.Id).ToHashSet();

            // Query ignore pairs where either station is in the region, and normalize ordering
            var allIgnoredPairs = await DbContext.StationMergeIgnores
                .AsNoTracking()
                .Where(i => allStationIds.Contains(i.Station1Id) || allStationIds.Contains(i.Station2Id))
                .Select(i => new { i.Station1Id, i.Station2Id })
                .ToListAsync();

            var ignoredSet = new HashSet<(int, int)>(
                allIgnoredPairs.Select(i => (Math.Min(i.Station1Id, i.Station2Id), Math.Max(i.Station1Id, i.Station2Id))));

            var allRegions = await DbContext.Regions
                .AsNoTracking()
                .Select(r => new { r.Id, r.Name, r.ParentRegionId })
                .ToListAsync();

            var regionDict = allRegions.ToDictionary(
                r => r.Id,
                r => (r.Name, ParentId: r.ParentRegionId));

            var result = stationsByRegion.Keys
                .Select(rId =>
                {
                    var stations = stationsByRegion[rId];
                    return new StationMergeCountryDTO
                    {
                        RegionId = rId,
                        RegionName = BuildRegionName(rId, regionDict),
                        PairCount = CountNearbyPairs(stations, ignoredSet)
                    };
                })
                .OrderBy(r => r.RegionName)
                .ToList();

            return Ok(result);
        }

        // Typed station data used for in-memory pair generation
        private sealed record StationData(int Id, string Name, double Lat, double Lon, bool Special, int Visits);

        /// <summary>
        /// Lazily enumerates pairs of nearby stations (within MaxDistanceMeters, inclusive)
        /// that are not in the ignore set. Stations must be pre-sorted by latitude.
        /// Uses a per-station latitude-based longitude bounding box to prevent false negatives
        /// at high latitudes.
        /// </summary>
        private static IEnumerable<StationNearbyPairDTO> EnumeratePairs(
            IList<StationData> stations,
            HashSet<(int, int)> ignoredSet)
        {
            for (int i = 0; i < stations.Count; i++)
            {
                var latI = stations[i].Lat;
                var lonBBox = LonBBoxDegrees(latI);
                for (int j = i + 1; j < stations.Count; j++)
                {
                    if (stations[j].Lat - latI > LatBBoxDegrees) break;
                    if (Math.Abs(stations[i].Lon - stations[j].Lon) > lonBBox) continue;

                    var dist = HaversineDistance(latI, stations[i].Lon, stations[j].Lat, stations[j].Lon);
                    if (dist > MaxDistanceMeters) continue;

                    var id1 = Math.Min(stations[i].Id, stations[j].Id);
                    var id2 = Math.Max(stations[i].Id, stations[j].Id);
                    if (ignoredSet.Contains((id1, id2))) continue;

                    yield return new StationNearbyPairDTO
                    {
                        Station1Id = stations[i].Id,
                        Station1Name = stations[i].Name,
                        Station1Lattitude = stations[i].Lat,
                        Station1Longitude = stations[i].Lon,
                        Station1Visits = stations[i].Visits,
                        Station1Special = stations[i].Special,
                        Station2Id = stations[j].Id,
                        Station2Name = stations[j].Name,
                        Station2Lattitude = stations[j].Lat,
                        Station2Longitude = stations[j].Lon,
                        Station2Visits = stations[j].Visits,
                        Station2Special = stations[j].Special,
                        DistanceMeters = Math.Round(dist, 1)
                    };
                }
            }
        }

        /// <summary>
        /// Returns paginated pairs of nearby stations (within 500 m) for a given region
        /// that have not yet been reviewed (not in the ignore list).
        /// Total is capped at MaxPairCount to prevent runaway counting on dense regions.
        /// </summary>
        [HttpGet("pairs/{regionId:int}")]
        public async Task<IActionResult> GetPairs(int regionId, [FromQuery] int page = 0, [FromQuery] int pageSize = 10)
        {
            if (!IsAdmin()) return Forbid();

            if (page < 0 || pageSize < 1 || pageSize > 100)
                return BadRequest("page must be >= 0 and pageSize must be between 1 and 100.");

            var stationsRaw = await DbContext.Stations
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
                .OrderBy(s => s.Lattitude)
                .ToListAsync();

            var stationIds = stationsRaw.Select(s => s.Id).ToHashSet();

            // Query ignore pairs where either station belongs to this region, normalize ordering
            var ignoredPairs = await DbContext.StationMergeIgnores
                .AsNoTracking()
                .Where(i => stationIds.Contains(i.Station1Id) || stationIds.Contains(i.Station2Id))
                .Select(i => new { i.Station1Id, i.Station2Id })
                .ToListAsync();

            var ignoredSet = new HashSet<(int, int)>(
                ignoredPairs.Select(i => (Math.Min(i.Station1Id, i.Station2Id), Math.Max(i.Station1Id, i.Station2Id))));

            var stationList = stationsRaw
                .Select(s => new StationData(s.Id, s.Name, s.Lattitude, s.Longitude, s.Special, s.Visits))
                .ToList();

            // Enumerate pairs lazily: collect the requested page; count up to MaxPairCount without
            // building a full DTO list for non-paged items.
            int skip = page * pageSize;
            int total = 0;
            var paged = new List<StationNearbyPairDTO>(pageSize);

            foreach (var pair in EnumeratePairs(stationList, ignoredSet))
            {
                if (total >= skip && paged.Count < pageSize)
                    paged.Add(pair);
                total++;
                if (total >= MaxPairCount) break;
            }

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
        /// Station1Id is always stored as min(id1, id2) to enforce canonical ordering.
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
