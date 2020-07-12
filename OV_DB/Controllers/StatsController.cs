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

            var query = _context.RouteInstances
                .Where(ri => ri.Route.RouteMaps.Any(rm => rm.Map.UserId == userIdClaim) && ri.Route.RouteMaps.Any(rm => rm.Map.MapGuid == map));

            if (year.HasValue)
            {
                query = query.Where(ri => ri.Date.Year == year);
            }

            var x = await query.Select(ri => new
            {
                ri.Date.Date,
                ri.Route.RouteType.Name,
                ri.Route.RouteType.NameNL,
                Distance = (double)(((ri.Route.OverrideDistance.HasValue && ri.Route.OverrideDistance > 0) ? ri.Route.OverrideDistance : ri.Route.CalculatedDistance))
            }).ToListAsync();

            var x2 = x.GroupBy(x => x.Name).Select(x => new { Name = x.Key, NameNl = x.First().NameNL, Distance = Math.Round(x.Sum(x => x.Distance), 2) }).OrderByDescending(x => x.Distance);
            return Ok(x2);
        }

        [HttpGet("time/{map}")]
        public async Task<ActionResult> GetTimedStats(Guid map, [FromQuery] int? year)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }

            var query = _context.RouteInstances
                .Where(ri => ri.Route.RouteMaps.Any(rm => rm.Map.UserId == userIdClaim) && ri.Route.RouteMaps.Any(rm => rm.Map.MapGuid == map));

            if (year.HasValue)
            {
                query = query.Where(ri => ri.Date.Year == year);
            }

            var x = await query.Select(ri => new
            {
                ri.Date.Date,
                ri.Route.RouteType.Name,
                ri.Route.RouteType.NameNL,
                Distance = (double)(((ri.Route.OverrideDistance.HasValue && ri.Route.OverrideDistance > 0) ? ri.Route.OverrideDistance : ri.Route.CalculatedDistance))
            }).OrderBy(x => x.Name).ThenBy(x => x.Date).ToListAsync();


            var typesAndValuesCumulative = new Dictionary<string, double>();
            var periodsCumulative = new Dictionary<string, Dictionary<DateTime, double>>();
            var periodsSingle = new Dictionary<string, Dictionary<DateTime, double>>();

            x.ForEach(value =>
            {
                if (!typesAndValuesCumulative.ContainsKey(value.Name))
                {
                    typesAndValuesCumulative.Add(value.Name, 0);
                    periodsCumulative.Add(value.Name, new Dictionary<DateTime, double>());
                    periodsSingle.Add(value.Name, new Dictionary<DateTime, double>());
                    if (year.HasValue)
                    {
                        periodsCumulative[value.Name].Add(new DateTime(year.Value, 1, 1), 0);
                        periodsSingle[value.Name].Add(new DateTime(year.Value, 1, 1), 0);
                    }
                }
                typesAndValuesCumulative[value.Name] += value.Distance;

                if (!periodsCumulative[value.Name].ContainsKey(value.Date.Date))
                {
                    periodsCumulative[value.Name].Add(value.Date.Date, 0);
                    periodsSingle[value.Name].Add(value.Date.Date, 0);

                }
                periodsCumulative[value.Name][value.Date.Date] = typesAndValuesCumulative[value.Name];
                periodsSingle[value.Name][value.Date.Date] += value.Distance;

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
                    dataCumulative.Datasets.Add(new Dataset { Label = k, Data = dataForKey });
                });
            var dates = periodsSingle.SelectMany(p => p.Value.Select(d => d.Key)).Distinct();
            periodsSingle.Keys.ToList().ForEach(k =>
                {
                    var dataForKey = periodsSingle[k].Select(x => new Point { T = x.Key, Y = Math.Round(x.Value, 2) }).ToList();
                    var dataToAdd = dates.Where(d => !dataForKey.Any(p => p.T == d)).ToList();
                    dataToAdd.ForEach(d => dataForKey.Add(new Point { T = d, Y = 0 }));
                    dataForKey = dataForKey.OrderBy(d => d.T).ToList();

                    dataSingle.Datasets.Add(new Dataset { Label = k, Data = dataForKey });
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

            var query = _context.RouteInstances
                .Where(ri => ri.Route.RouteMaps.Any(rm => rm.Map.UserId == userIdClaim) && ri.Route.RouteMaps.Any(rm => rm.Map.MapGuid == map));

            if (year.HasValue)
            {
                query = query.Where(ri => ri.Date.Year == year);
            }

            var x = await query.Select(ri => ri.Route).Distinct().ToListAsync();
            if (!x.Any())
            {
                return Ok();
            }

            var x2 = x.Select(c =>
            new
            {
                Route = c,
                Coordinates = c.Coordinates.Split("\n").Select(c2 => c2.Replace("\r", "").Split(',').Select(x => double.Parse(x, CultureInfo.InvariantCulture)))
            }).ToList();


            var x3 = x2.Select(route =>
              {
                  return new
                  {
                      Route = route,
                      MinLat = route.Coordinates.OrderBy(c => c.ElementAt(1)).First(),
                      MaxLat = route.Coordinates.OrderByDescending(c => c.ElementAt(1)).First(),
                      MinLong = route.Coordinates.OrderBy(c => c.First()).First(),
                      MaxLong = route.Coordinates.OrderByDescending(c => c.First()).First()
                  };
              }).ToList();

            var minLatPoint = x3.OrderBy(x => x.MinLat.ElementAt(1)).Select(x =>
            {
                return new BoundsPoint
                {
                    Lat = x.MinLat.ElementAt(1),
                    Long = x.MinLat.First(),
                    Route = x.Route.Route
                };
            }).First();

            var maxLatPoint = x3.OrderByDescending(x => x.MaxLat.ElementAt(1)).Select(x =>
            {
                return new BoundsPoint
                {
                    Lat = x.MaxLat.ElementAt(1),
                    Long = x.MaxLat.First(),
                    Route = x.Route.Route
                };
            }).First();

            var minLongPoint = x3.OrderBy(x => x.MinLong.First()).Select(x =>
        {
            return new BoundsPoint
            {
                Lat = x.MinLong.ElementAt(1),
                Long = x.MinLong.First(),
                Route = x.Route.Route
            };
        }).First();

            var maxLongPoint = x3.OrderByDescending(x => x.MaxLong.First()).Select(x =>
            {
                return new BoundsPoint
                {
                    Lat = x.MaxLong.ElementAt(1),
                    Long = x.MaxLong.First(),
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
