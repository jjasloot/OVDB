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
                    var dataForKey = periodsCumulative[k].Select(x => new Point { T = x.Key, Y = Math.Round(x.Value, 2) }).ToList();
                    var colour = typesAndColours[k].ToUpper();
                    dataCumulative.Datasets.Add(new Dataset { Label = k, Data = dataForKey, PointBackgroundColor = colour, BorderColor = colour });
                });
            var dates = periodsSingle.SelectMany(p => p.Value.Select(d => d.Key)).Distinct();
            periodsSingle.Keys.ToList().ForEach(k =>
                {
                    var dataForKey = periodsSingle[k].Select(x => new Point { T = x.Key, Y = Math.Round(x.Value, 2) }).ToList();
                    var dataToAdd = dates.Where(d => !dataForKey.Any(p => p.T == d)).ToList();
                    dataToAdd.ForEach(d => dataForKey.Add(new Point { T = d, Y = 0 }));
                    dataForKey = dataForKey.OrderBy(d => d.T).ToList();
                    var colour = typesAndColours[k].ToUpper();
                    dataSingle.Datasets.Add(new Dataset { Label = k, Data = dataForKey, BorderColor = colour, PointBackgroundColor = colour });
                });

            if (year.HasValue)
            {
                var endDate = new DateTime(year.Value + 1, 1, 1);
                if (endDate > DateTime.Now)
                {
                    endDate = DateTime.Now.AddDays(1).Date;
                }
                dataCumulative.Datasets.ForEach(ds => ds.Data.Add(new Point { T = endDate, Y = Math.Round(typesAndValuesCumulative[ds.Label], 2) }));
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
                      MinLat = route.Coordinates.OrderBy(c => c.X).First(),
                      MaxLat = route.Coordinates.OrderByDescending(c => c.X).First(),
                      MinLong = route.Coordinates.OrderBy(c => c.Y).First(),
                      MaxLong = route.Coordinates.OrderByDescending(c => c.Y).First()
                  };
              }).ToList();

            var minLatPoint = x3.OrderBy(x => x.MinLat.X).Select(x =>
            {
                return new BoundsPoint
                {
                    Lat = x.MinLat.X,
                    Long = x.MinLat.Y,
                    Route = x.Route.Route
                };
            }).First();

            var maxLatPoint = x3.OrderByDescending(x => x.MaxLat.X).Select(x =>
            {
                return new BoundsPoint
                {
                    Lat = x.MaxLat.X,
                    Long = x.MaxLat.Y,
                    Route = x.Route.Route
                };
            }).First();

            var minLongPoint = x3.OrderBy(x => x.MinLong.Y).Select(x =>
        {
            return new BoundsPoint
            {
                Lat = x.MinLong.X,
                Long = x.MinLong.Y,
                Route = x.Route.Route
            };
        }).First();

            var maxLongPoint = x3.OrderByDescending(x => x.MaxLong.Y).Select(x =>
            {
                return new BoundsPoint
                {
                    Lat = x.MaxLong.X,
                    Long = x.MaxLong.Y,
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
    }
}
