using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
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
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
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
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
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

        [HttpGet("reach/{map}")]
        public async Task<ActionResult> GetReachStats(Guid map, [FromQuery] int? year)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }

            var query = QueryForInstances(map, year, userIdClaim);

            var x = await query.Select(ri => ri.Route).Distinct().ToListAsync();
            if (!x.Any())
            {
                return Ok();
            }

            var x2 = x.Select(c =>
            new
            {
                Route = c,
                Coordinates = c.LineString.Coordinates
            }).ToList();


            var x3 = x2.Select(route =>
              {
                  return new
                  {
                      Route = route,
                      MinLat = route.Coordinates.OrderBy(c => c.Y).First(),
                      MaxLat = route.Coordinates.OrderByDescending(c => c.Y).First(),
                      MinLong = route.Coordinates.OrderBy(c => c.X).First(),
                      MaxLong = route.Coordinates.OrderByDescending(c => c.X).First()
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
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
                return Forbid();

            // Get all visited station ids for user
            var visitedStationIds = await _context.StationVisits
                .Where(sv => sv.UserId == userIdClaim)
                .Select(sv => sv.StationId)
                .ToListAsync();

            // Fetch all regions and their minimal station data in one query
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
                    Stations = r.Stations.Select(s => new { s.Id, s.Hidden, s.Special }).ToList()
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
                VisitedStations = r.Stations.Count(s => !s.Hidden && !s.Special && visitedStationIds.Contains(s.Id)),
                TotalStations = r.Stations.Count(s => !s.Hidden && !s.Special),
                FlagEmoji = r.FlagEmoji,
                ParentRegionId = r.ParentRegionId,
                // Children will be filled in next step
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
                    region.Visited = region.VisitedStations > 0;
                    if (!region.Visited)
                    {
                        region.Children = [];
                    }
                    region.Children = FilterRegions(region.Children);
                    result.Add(region);
                }
                return result;
            }

            var stats = FilterRegions(topLevel);
            return Ok(stats);
        }
    }
}
