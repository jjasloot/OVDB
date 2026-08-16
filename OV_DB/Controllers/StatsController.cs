using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OV_DB.Models;
using OV_DB.Models.Graphs;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class StatsController : ControllerBase
    {
        private readonly OVDBDatabaseContext _context;

        public StatsController(OVDBDatabaseContext context)
        {
            _context = context;
        }

        [HttpGet("{map}")]
        public async Task<ActionResult> GetStats(Guid map, [FromQuery] int? year)
        {
            var userIdClaim = User.GetUserId();
            if (userIdClaim < 0)
            {
                return Forbid();
            }

            var query = QueryForInstances(map, year, userIdClaim);

            var x = await query.Select(ri => new
            {
                ri.Date.Date,
                ri.Route.RouteType.Name,
                ri.Route.RouteType.NameNL,
                Distance = (double)(((ri.Route.OverrideDistance.HasValue && ri.Route.OverrideDistance > 0) ? ri.Route.OverrideDistance : ri.Route.CalculatedDistance))
            }).ToListAsync();

            var x2 = x.GroupBy(x => x.Name).Select(x => new { Name = x.Key, x.First().NameNL, Distance = Math.Round(x.Sum(x => x.Distance), 2) }).OrderByDescending(x => x.Distance);
            return Ok(x2);
        }

        private IQueryable<RouteInstance> QueryForInstances(Guid map, int? year, int userIdClaim)
        {
            var query = _context.RouteInstances
                .AsNoTracking()
                .Where(ri => ri.Route.RouteMaps.Any(rm => rm.Map.UserId == userIdClaim) && (ri.Route.RouteMaps.Any(rm => rm.Map.MapGuid == map) || ri.RouteInstanceMaps.Any(rim => rim.Map.MapGuid == map)));

            if (year.HasValue)
            {
                query = query.Where(ri => ri.Date.Year == year);
            }

            return query;
        }

        [HttpGet("time/{map}")]
        public async Task<ActionResult> GetTimedStats(Guid map, [FromQuery] int? year, [FromQuery] string language = "nl")
        {
            var userIdClaim = User.GetUserId();
            if (userIdClaim < 0)
            {
                return Forbid();
            }

            var query = QueryForInstances(map, year, userIdClaim);

            var x = await query.Select(ri => new
            {
                ri.Date.Date,
                ri.Route.RouteType.Name,
                ri.Route.RouteType.NameNL,
                ri.Route.RouteType.Colour,
                Distance = (double)(((ri.Route.OverrideDistance.HasValue && ri.Route.OverrideDistance > 0) ? ri.Route.OverrideDistance : ri.Route.CalculatedDistance))
            }).OrderBy(x => x.Name).ThenBy(x => x.Date).ToListAsync();


            var typesAndValuesCumulative = new Dictionary<string, double>();
            var typesAndColours = new Dictionary<string, string>();
            var periodsCumulative = new Dictionary<string, Dictionary<DateTime, double>>();
            var periodsSingle = new Dictionary<string, Dictionary<DateTime, double>>();

            x.ForEach(value =>
            {
                var name = value.Name;
                if (language == "nl" && !string.IsNullOrWhiteSpace(value.NameNL))
                {
                    name = value.NameNL;
                }
                if (!typesAndColours.ContainsKey(name))
                {
                    typesAndColours.Add(name, value.Colour);
                }
                if (!typesAndValuesCumulative.ContainsKey(name))
                {

                    typesAndValuesCumulative.Add(name, 0);
                    periodsCumulative.Add(name, new Dictionary<DateTime, double>());
                    periodsSingle.Add(name, new Dictionary<DateTime, double>());
                    if (year.HasValue)
                    {
                        periodsCumulative[name].Add(new DateTime(year.Value, 1, 1), 0);
                        periodsSingle[name].Add(new DateTime(year.Value, 1, 1), 0);
                    }
                }
                typesAndValuesCumulative[name] += value.Distance;

                if (!periodsCumulative[name].ContainsKey(value.Date.Date))
                {
                    periodsCumulative[name].Add(value.Date.Date, 0);
                    periodsSingle[name].Add(value.Date.Date, 0);

                }
                periodsCumulative[name][value.Date.Date] = typesAndValuesCumulative[name];
                periodsSingle[name][value.Date.Date] += value.Distance;

            });
            var dataCumulative = new Data
            {
                Datasets = new List<Dataset>()
            };
            var dataSingle = new Data
            {
                Datasets = new List<Dataset>()
            };
            periodsCumulative.Keys.ToList().ForEach(k =>
                {
                    var dataForKey = periodsCumulative[k].Select(x => new Point { X = x.Key.ToString("yyyy-MM-dd"), Y = Math.Round(x.Value, 2) }).ToList();
                    var colour = typesAndColours[k].ToUpper();
                    dataCumulative.Datasets.Add(new Dataset { Label = k, Data = dataForKey, BackgroundColor = colour, BorderColor = colour, Stack = false, Fill = false });
                });
            var dates = periodsSingle.SelectMany(p => p.Value.Select(d => d.Key)).Distinct();
            periodsSingle.Keys.ToList().ForEach(k =>
                {
                    var dataForKey = periodsSingle[k].Select(x => new Point { X = x.Key.ToString("yyyy-MM-dd"), Y = Math.Round(x.Value, 2) }).ToList();
                    var dataToAdd = dates.Where(d => !dataForKey.Any(p => p.X == d.ToString("yyyy-MM-dd"))).ToList();
                    dataToAdd.ForEach(d => dataForKey.Add(new Point { X = d.ToString("yyyy-MM-dd"), Y = 0 }));
                    dataForKey = dataForKey.OrderBy(d => d.X).ToList();
                    var colour = typesAndColours[k].ToUpper();
                    dataSingle.Datasets.Add(new Dataset { Label = k, Data = dataForKey, BorderColor = colour, BackgroundColor = colour, Fill = true });
                });

            if (year.HasValue)
            {
                var endDate = new DateTime(year.Value + 1, 1, 1);
                if (endDate > DateTime.Now)
                {
                    endDate = DateTime.Now.AddDays(1).Date;
                }
                dataCumulative.Datasets.ForEach(ds => ds.Data.Add(new Point { X = endDate.ToString("yyyy-MM-dd"), Y = Math.Round(typesAndValuesCumulative[ds.Label], 2) }));
            }
            return Ok(new { Cumulative = dataCumulative, Single = dataSingle });
        }

        private static readonly string[] DelayBucketKeys = ["EARLY", "ONTIME", "D5_15", "D15_30", "D30_60", "D60PLUS"];

        /// <summary>
        /// The stations in a region the user has not visited yet. Uses the same hidden/special
        /// exclusions as the region completion counts, so the numbers agree with the progress bars.
        /// </summary>
        [HttpGet("region/{regionId:int}/missing-stations")]
        public async Task<ActionResult<List<MissingStationDTO>>> GetMissingStations(int regionId, [FromQuery] int limit = 250)
        {
            var userIdClaim = User.GetUserId();
            if (userIdClaim < 0)
            {
                return Forbid();
            }

            limit = Math.Clamp(limit, 1, 1000);

            var stations = await _context.Stations
                .AsNoTracking()
                .Where(s => s.Regions.Any(r => r.Id == regionId))
                .Where(s => !s.Hidden && !s.Special)
                .Where(s => !s.StationVisits.Any(sv => sv.UserId == userIdClaim))
                .OrderBy(s => s.Name)
                .Take(limit)
                .Select(s => new MissingStationDTO
                {
                    Id = s.Id,
                    Name = s.Name,
                    Latitude = s.Lattitude,
                    Longitude = s.Longitude
                })
                .ToListAsync();

            return Ok(stations);
        }

        [HttpGet("year-in-review/{map}")]
        public async Task<ActionResult<YearInReviewDTO>> GetYearInReview(Guid map, [FromQuery] int? year)
        {
            var userIdClaim = User.GetUserId();
            if (userIdClaim < 0)
            {
                return Forbid();
            }

            const int onTimeThresholdMinutes = 5;
            var targetYear = year ?? DateTime.Now.Year;

            var trips = await QueryForInstances(map, targetYear, userIdClaim)
                .Select(ri => new
                {
                    ri.RouteId,
                    ri.Date,
                    ri.DurationHours,
                    ri.EndTime,
                    ri.ScheduledEndTime,
                    ri.Route.Name,
                    ri.Route.NameNL,
                    Operator = ri.Route.OperatingCompany,
                    TypeName = ri.Route.RouteType.Name,
                    TypeNameNL = ri.Route.RouteType.NameNL,
                    Distance = (double)((ri.Route.OverrideDistance.HasValue && ri.Route.OverrideDistance > 0)
                        ? ri.Route.OverrideDistance
                        : ri.Route.CalculatedDistance)
                })
                .ToListAsync();

            var result = new YearInReviewDTO
            {
                Year = targetYear,
                Trips = trips.Count,
                DistanceKm = Math.Round(trips.Sum(t => t.Distance), 1),
                DurationHours = Math.Round(trips.Sum(t => t.DurationHours ?? 0), 1),
                ActiveDays = trips.Select(t => t.Date.Date).Distinct().Count(),
                DistinctRoutes = trips.Select(t => t.RouteId).Distinct().Count(),
                MonthlyDistanceKm = Enumerable.Range(1, 12)
                    .Select(month => Math.Round(trips.Where(t => t.Date.Month == month).Sum(t => t.Distance), 1))
                    .ToList()
            };

            var routeIds = trips.Select(t => t.RouteId).Distinct().ToList();

            if (routeIds.Count > 0)
            {
                // A route counts as new when its earliest instance anywhere falls in this year.
                var firstRides = await _context.RouteInstances
                    .AsNoTracking()
                    .Where(ri => routeIds.Contains(ri.RouteId))
                    .GroupBy(ri => ri.RouteId)
                    .Select(g => new { RouteId = g.Key, First = g.Min(ri => ri.Date) })
                    .ToListAsync();
                result.NewRoutes = firstRides.Count(f => f.First.Year == targetYear);

                result.Countries = await _context.Routes
                    .AsNoTracking()
                    .Where(r => routeIds.Contains(r.RouteId))
                    .SelectMany(r => r.Regions)
                    .Where(r => r.IsoCode != null && r.IsoCode != "")
                    .Select(r => new CountryVisitDTO
                    {
                        IsoCode = r.IsoCode,
                        FlagEmoji = r.FlagEmoji,
                        Name = r.Name,
                        NameNL = r.NameNL
                    })
                    .Distinct()
                    .OrderBy(c => c.Name)
                    .ToListAsync();
            }

            result.TopRouteTypes = trips
                .Where(t => !string.IsNullOrWhiteSpace(t.TypeName))
                .GroupBy(t => new { t.TypeName, t.TypeNameNL })
                .Select(g => new NameCountDTO
                {
                    Name = g.Key.TypeName,
                    NameNL = g.Key.TypeNameNL,
                    Trips = g.Count(),
                    DistanceKm = Math.Round(g.Sum(t => t.Distance), 1)
                })
                .OrderByDescending(g => g.DistanceKm)
                .ToList();

            result.TopOperators = trips
                .Where(t => !string.IsNullOrWhiteSpace(t.Operator))
                .GroupBy(t => t.Operator.Trim())
                .Select(g => new NameCountDTO
                {
                    Name = g.Key,
                    NameNL = g.Key,
                    Trips = g.Count(),
                    DistanceKm = Math.Round(g.Sum(t => t.Distance), 1)
                })
                .OrderByDescending(g => g.Trips)
                .ThenByDescending(g => g.DistanceKm)
                .Take(5)
                .ToList();

            var longest = trips.OrderByDescending(t => t.Distance).FirstOrDefault();
            if (longest != null && longest.Distance > 0)
            {
                result.LongestTrip = new HighlightTripDTO
                {
                    RouteId = longest.RouteId,
                    Date = longest.Date,
                    Name = longest.Name,
                    NameNL = longest.NameNL,
                    DistanceKm = Math.Round(longest.Distance, 1),
                    DurationHours = longest.DurationHours,
                    AverageSpeedKmh = longest.DurationHours > 0
                        ? Math.Round(longest.Distance / longest.DurationHours.Value, 1)
                        : null
                };
            }

            var fastest = trips
                .Where(t => t.DurationHours > 0 && t.Distance > 0)
                .OrderByDescending(t => t.Distance / t.DurationHours.Value)
                .FirstOrDefault();
            if (fastest != null)
            {
                result.FastestTrip = new HighlightTripDTO
                {
                    RouteId = fastest.RouteId,
                    Date = fastest.Date,
                    Name = fastest.Name,
                    NameNL = fastest.NameNL,
                    DistanceKm = Math.Round(fastest.Distance, 1),
                    DurationHours = fastest.DurationHours,
                    AverageSpeedKmh = Math.Round(fastest.Distance / fastest.DurationHours.Value, 1)
                };
            }

            var busiest = trips
                .GroupBy(t => t.Date.Date)
                .Select(g => new BusiestDayDTO
                {
                    Date = g.Key,
                    Trips = g.Count(),
                    DistanceKm = Math.Round(g.Sum(t => t.Distance), 1)
                })
                .OrderByDescending(d => d.Trips)
                .ThenByDescending(d => d.DistanceKm)
                .FirstOrDefault();
            result.BusiestDay = busiest;

            var arrivalDelays = trips
                .Where(t => t.EndTime.HasValue && t.ScheduledEndTime.HasValue)
                .Select(t => (t.EndTime.Value - t.ScheduledEndTime.Value).TotalMinutes)
                .ToList();
            result.TripsWithArrivalData = arrivalDelays.Count;
            if (arrivalDelays.Count > 0)
            {
                result.AverageArrivalDelayMinutes = Math.Round(arrivalDelays.Average(), 1);
                result.OnTimePercentage = Math.Round(
                    100.0 * arrivalDelays.Count(d => d < onTimeThresholdMinutes) / arrivalDelays.Count, 1);
            }

            var previousQuery = QueryForInstances(map, targetYear - 1, userIdClaim);
            result.PreviousYearTrips = await previousQuery.CountAsync();
            result.PreviousYearDistanceKm = Math.Round(
                await previousQuery.SumAsync(ri => (double?)((ri.Route.OverrideDistance.HasValue && ri.Route.OverrideDistance > 0)
                    ? ri.Route.OverrideDistance
                    : ri.Route.CalculatedDistance)) ?? 0, 1);

            return Ok(result);
        }

        [HttpGet("punctuality/{map}")]
        public async Task<ActionResult<PunctualityStatsDTO>> GetPunctualityStats(Guid map, [FromQuery] int? year)
        {
            var userIdClaim = User.GetUserId();
            if (userIdClaim < 0)
            {
                return Forbid();
            }

            const int onTimeThresholdMinutes = 5;
            var query = QueryForInstances(map, year, userIdClaim);

            var totalTrips = await query.CountAsync();

            // DepartureDelayMinutes/ArrivalDelayMinutes are computed C# properties, so EF cannot
            // translate them - pull the raw times for trips that have them and aggregate here.
            var rows = await query
                .Where(ri => (ri.StartTime.HasValue && ri.ScheduledStartTime.HasValue)
                             || (ri.EndTime.HasValue && ri.ScheduledEndTime.HasValue))
                .Select(ri => new
                {
                    ri.RouteInstanceId,
                    ri.RouteId,
                    ri.Date,
                    ri.StartTime,
                    ri.ScheduledStartTime,
                    ri.EndTime,
                    ri.ScheduledEndTime,
                    ri.Route.Name,
                    ri.Route.NameNL,
                    Operator = ri.Route.OperatingCompany
                })
                .ToListAsync();

            var departureDelays = rows
                .Where(r => r.StartTime.HasValue && r.ScheduledStartTime.HasValue)
                .Select(r => (r.StartTime.Value - r.ScheduledStartTime.Value).TotalMinutes)
                .ToList();

            var arrivals = rows
                .Where(r => r.EndTime.HasValue && r.ScheduledEndTime.HasValue)
                .Select(r => new
                {
                    r.RouteInstanceId,
                    r.RouteId,
                    r.Date,
                    r.Name,
                    r.NameNL,
                    r.Operator,
                    Delay = (r.EndTime.Value - r.ScheduledEndTime.Value).TotalMinutes
                })
                .ToList();

            var result = new PunctualityStatsDTO
            {
                TotalTrips = totalTrips,
                TripsWithDepartureData = departureDelays.Count,
                TripsWithArrivalData = arrivals.Count,
                OnTimeThresholdMinutes = onTimeThresholdMinutes,
                AverageDepartureDelayMinutes = departureDelays.Count > 0 ? Math.Round(departureDelays.Average(), 1) : null,
                AverageArrivalDelayMinutes = arrivals.Count > 0 ? Math.Round(arrivals.Average(a => a.Delay), 1) : null,
                MedianArrivalDelayMinutes = Median(arrivals.Select(a => a.Delay).ToList()),
                OnTimePercentage = arrivals.Count > 0
                    ? Math.Round(100.0 * arrivals.Count(a => a.Delay < onTimeThresholdMinutes) / arrivals.Count, 1)
                    : null,
                ArrivalDelayDistribution = BucketDelays(arrivals.Select(a => a.Delay))
            };

            result.ByOperator = arrivals
                .Where(a => !string.IsNullOrWhiteSpace(a.Operator))
                .GroupBy(a => a.Operator.Trim())
                .Select(g => new GroupPunctualityDTO
                {
                    Label = g.Key,
                    Trips = g.Count(),
                    AverageArrivalDelayMinutes = Math.Round(g.Average(a => a.Delay), 1),
                    OnTimePercentage = Math.Round(100.0 * g.Count(a => a.Delay < onTimeThresholdMinutes) / g.Count(), 1)
                })
                .OrderByDescending(g => g.Trips)
                .ThenBy(g => g.Label)
                .Take(15)
                .ToList();

            result.ByYear = arrivals
                .GroupBy(a => a.Date.Year)
                .Select(g => new GroupPunctualityDTO
                {
                    Label = g.Key.ToString(CultureInfo.InvariantCulture),
                    Trips = g.Count(),
                    AverageArrivalDelayMinutes = Math.Round(g.Average(a => a.Delay), 1),
                    OnTimePercentage = Math.Round(100.0 * g.Count(a => a.Delay < onTimeThresholdMinutes) / g.Count(), 1)
                })
                .OrderBy(g => g.Label)
                .ToList();

            result.WorstTrips = arrivals
                .OrderByDescending(a => a.Delay)
                .Take(10)
                .Select(a => new DelayedTripDTO
                {
                    RouteInstanceId = a.RouteInstanceId,
                    RouteId = a.RouteId,
                    Date = a.Date,
                    Name = a.Name,
                    NameNL = a.NameNL,
                    Operator = a.Operator,
                    DelayMinutes = Math.Round(a.Delay, 1)
                })
                .ToList();

            return Ok(result);
        }

        internal static double? Median(List<double> values)
        {
            if (values.Count == 0)
            {
                return null;
            }
            var sorted = values.OrderBy(v => v).ToList();
            var mid = sorted.Count / 2;
            return Math.Round(sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2 : sorted[mid], 1);
        }

        internal static List<DelayBucketDTO> BucketDelays(IEnumerable<double> delays)
        {
            var counts = new int[DelayBucketKeys.Length];
            foreach (var delay in delays)
            {
                var index = delay < -1 ? 0
                    : delay < 5 ? 1
                    : delay < 15 ? 2
                    : delay < 30 ? 3
                    : delay < 60 ? 4
                    : 5;
                counts[index]++;
            }
            return DelayBucketKeys.Select((key, i) => new DelayBucketDTO { Key = key, Count = counts[i] }).ToList();
        }

        [HttpGet("reach/{map}")]
        public async Task<ActionResult> GetReachStats(Guid map, [FromQuery] int? year)
        {
            var userIdClaim = User.GetUserId();
            if (userIdClaim < 0)
            {
                return Forbid();
            }

            var query = QueryForInstances(map, year, userIdClaim);

            // Distinct on the int key instead of on the whole Route entity, which otherwise
            // makes MySQL run SELECT DISTINCT over every column including the geometry blob.
            var routeIds = await query.Select(ri => ri.RouteId).Distinct().ToListAsync();
            if (routeIds.Count == 0)
            {
                return Ok();
            }

            var x = await _context.Routes
                .AsNoTracking()
                .Where(r => routeIds.Contains(r.RouteId))
                .ToListAsync();

            var x2 = x.Select(c =>
            new
            {
                Route = c,
                Coordinates = c.LineString.Coordinates
            }).ToList();


            var x3 = x2.Where(route => route.Coordinates.Length > 0).Select(route =>
              {
                  // Use Min/Max instead of OrderBy for better performance
                  var minLat = route.Coordinates[0];
                  var maxLat = route.Coordinates[0];
                  var minLong = route.Coordinates[0];
                  var maxLong = route.Coordinates[0];

                  foreach (var coord in route.Coordinates)
                  {
                      if (coord.Y < minLat.Y) minLat = coord;
                      if (coord.Y > maxLat.Y) maxLat = coord;
                      if (coord.X < minLong.X) minLong = coord;
                      if (coord.X > maxLong.X) maxLong = coord;
                  }

                  return new
                  {
                      Route = route,
                      MinLat = minLat,
                      MaxLat = maxLat,
                      MinLong = minLong,
                      MaxLong = maxLong
                  };
              }).ToList();

            var minLatPoint = x3.OrderBy(x => x.MinLat.Y).Select(x =>
            {
                return new BoundsPoint
                {
                    Lat = x.MinLat.Y,
                    Long = x.MinLat.X,
                    Route = x.Route.Route
                };
            }).First();

            var maxLatPoint = x3.OrderByDescending(x => x.MaxLat.Y).Select(x =>
            {
                return new BoundsPoint
                {
                    Lat = x.MaxLat.Y,
                    Long = x.MaxLat.X,
                    Route = x.Route.Route
                };
            }).First();

            var minLongPoint = x3.OrderBy(x => x.MinLong.X).Select(x =>
        {
            return new BoundsPoint
            {
                Lat = x.MinLong.Y,
                Long = x.MinLong.X,
                Route = x.Route.Route
            };
        }).First();

            var maxLongPoint = x3.OrderByDescending(x => x.MaxLong.X).Select(x =>
            {
                return new BoundsPoint
                {
                    Lat = x.MaxLong.Y,
                    Long = x.MaxLong.X,
                    Route = x.Route.Route
                };
            }).First();

            var bounds = new Bounds
            {
                LatMax = maxLatPoint,
                LatMin = minLatPoint,
                LongMax = maxLongPoint,
                LongMin = minLongPoint
            };
            return Ok(bounds);
        }

        [HttpGet("region")]
        public async Task<ActionResult<List<RegionStatDTO>>> GetRegionStats()
        {
            var userIdClaim = User.GetUserId();
            if (userIdClaim < 0)
                return Forbid();

            // Get all region IDs that are in any of the user's routes
            var userRouteRegionIds = await _context.Routes
                .Where(r => r.RouteMaps.Any(rm => rm.Map.UserId == userIdClaim))
                .SelectMany(r => r.Regions.Select(region => region.Id))
                .Distinct()
                .ToListAsync();

            // Count stations per region in SQL instead of shipping every station row to the app.
            var regions = await _context.Regions
                .Select(r => new
                {
                    r.Id,
                    r.Name,
                    r.NameNL,
                    r.OriginalName,
                    r.OsmRelationId,
                    r.ParentRegionId,
                    r.FlagEmoji,
                    TotalStations = r.Stations.Count(s => !s.Hidden && !s.Special),
                    VisitedStations = r.Stations.Count(s => !s.Hidden && !s.Special && s.StationVisits.Any(sv => sv.UserId == userIdClaim))
                })
                .ToListAsync();

            // Build a dictionary for fast lookup
            var regionDict = regions.ToDictionary(r => r.Id);

            // Prepare RegionStatDTOs
            var regionDtos = regions.Select(r => new RegionStatDTO
            {
                Id = r.Id,
                Name = r.Name,
                NameNL = r.NameNL,
                OriginalName = r.OriginalName,
                OsmRelationId = r.OsmRelationId,
                VisitedStations = r.VisitedStations,
                TotalStations = r.TotalStations,
                FlagEmoji = r.FlagEmoji,
                ParentRegionId = r.ParentRegionId,
                // Visited is now based on user's routes (any route in this region)
                Visited = userRouteRegionIds.Contains(r.Id),
                Children = new List<RegionStatDTO>()
            }).ToList();

            // Map region ID to DTO for fast lookup
            var dtoDict = regionDtos.ToDictionary(r => r.Id);

            // Assign children
            foreach (var dto in regionDtos)
            {
                var parentId = regionDict[dto.Id].ParentRegionId;
                if (parentId.HasValue && dtoDict.TryGetValue(parentId.Value, out var parentDto))
                {
                    parentDto.Children.Add(dto);
                }
            }

            // Only return top-level regions (no parent)
            var topLevel = regionDtos.Where(r => !regionDict[r.Id].ParentRegionId.HasValue).ToList();

            // Recursively filter children to only include visited or have visited children, or always show top-level
            List<RegionStatDTO> FilterRegions(List<RegionStatDTO> input)
            {
                var result = new List<RegionStatDTO>();
                foreach (var region in input)
                {
                    if (!region.Visited)
                    {
                        region.Children = [];
                    }
                    region.Children = FilterRegions(region.Children);
                    result.Add(region);
                }
                return result;
            }

            var stats = FilterRegions(topLevel).OrderBy(r=>r.OriginalName);
            return Ok(stats);
        }
    }
}
