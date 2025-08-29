using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AnalyticsController : ControllerBase
    {
        private readonly OVDBDatabaseContext _context;

        public AnalyticsController(OVDBDatabaseContext context)
        {
            _context = context;
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : null;
        }

        private IQueryable<RouteInstance> GetUserRouteInstances(Guid mapGuid, int? year = null)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return _context.RouteInstances.Where(x => false);

            var query = _context.RouteInstances
                .Include(ri => ri.Route)
                    .ThenInclude(r => r.RouteType)
                .Include(ri => ri.RouteInstanceProperties)
                .Where(ri => ri.Route.RouteMaps.Any(rm => rm.Map.UserId == userId.Value) && 
                           (ri.Route.RouteMaps.Any(rm => rm.Map.MapGuid == mapGuid) || 
                            ri.RouteInstanceMaps.Any(rim => rim.Map.MapGuid == mapGuid)));

            if (year.HasValue)
            {
                query = query.Where(ri => ri.Date.Year == year.Value);
            }

            return query;
        }

        /// <summary>
        /// Get trip frequency data over time with date range filters
        /// </summary>
        [HttpGet("trip-frequency/{mapGuid}")]
        public async Task<IActionResult> GetTripFrequency(Guid mapGuid, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] int? year)
        {
            var query = GetUserRouteInstances(mapGuid, year);
            
            if (startDate.HasValue)
                query = query.Where(ri => ri.Date >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(ri => ri.Date <= endDate.Value);

            var tripData = await query
                .GroupBy(ri => ri.Date.Date)
                .Select(g => new 
                {
                    Date = g.Key,
                    TripCount = g.Count(),
                    TotalDistance = g.Sum(ri => (double)(ri.Route.OverrideDistance ?? ri.Route.CalculatedDistance))
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            return Ok(tripData);
        }

        /// <summary>
        /// Get GitHub-style activity heatmap data
        /// </summary>
        [HttpGet("activity-heatmap/{mapGuid}")]
        public async Task<IActionResult> GetActivityHeatmap(Guid mapGuid, [FromQuery] int? year)
        {
            var targetYear = year ?? DateTime.Now.Year;
            var startDate = new DateTime(targetYear, 1, 1);
            var endDate = new DateTime(targetYear, 12, 31);

            var query = GetUserRouteInstances(mapGuid);
            
            var activityData = await query
                .Where(ri => ri.Date >= startDate && ri.Date <= endDate)
                .GroupBy(ri => ri.Date.Date)
                .Select(g => new 
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    TripCount = g.Count(),
                    TotalDistance = g.Sum(ri => (double)(ri.Route.OverrideDistance ?? ri.Route.CalculatedDistance))
                })
                .ToListAsync();

            // Create complete year data with zeros for missing dates
            var allDates = new List<object>();
            var activityDict = activityData.ToDictionary(x => x.Date);
            
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var dateStr = date.ToString("yyyy-MM-dd");
                if (activityDict.TryGetValue(dateStr, out var activity))
                {
                    allDates.Add(new 
                    {
                        Date = dateStr,
                        TripCount = activity.TripCount,
                        TotalDistance = Math.Round(activity.TotalDistance, 2),
                        Level = GetActivityLevel(activity.TripCount)
                    });
                }
                else
                {
                    allDates.Add(new 
                    {
                        Date = dateStr,
                        TripCount = 0,
                        TotalDistance = 0.0,
                        Level = 0
                    });
                }
            }

            return Ok(new { Year = targetYear, Data = allDates });
        }

        private int GetActivityLevel(int tripCount)
        {
            if (tripCount == 0) return 0;
            if (tripCount <= 2) return 1;
            if (tripCount <= 5) return 2;
            if (tripCount <= 10) return 3;
            return 4;
        }

        /// <summary>
        /// Get most used routes and stations rankings
        /// </summary>
        [HttpGet("route-rankings/{mapGuid}")]
        public async Task<IActionResult> GetRouteRankings(Guid mapGuid, [FromQuery] int? year, [FromQuery] int limit = 20)
        {
            var query = GetUserRouteInstances(mapGuid, year);
            
            var routeStats = await query
                .GroupBy(ri => new { ri.RouteId, RouteName = ri.Route.Name, RouteTypeName = ri.Route.RouteType.Name, RouteTypeNameNL = ri.Route.RouteType.NameNL })
                .Select(g => new 
                {
                    RouteId = g.Key.RouteId,
                    RouteName = g.Key.RouteName,
                    RouteType = g.Key.RouteTypeName,
                    RouteTypeNameNL = g.Key.RouteTypeNameNL,
                    TripCount = g.Count(),
                    TotalDistance = g.Sum(ri => (double)(ri.Route.OverrideDistance ?? ri.Route.CalculatedDistance)),
                    AverageDistance = g.Average(ri => (double)(ri.Route.OverrideDistance ?? ri.Route.CalculatedDistance)),
                    FirstTrip = g.Min(ri => ri.Date),
                    LastTrip = g.Max(ri => ri.Date)
                })
                .OrderByDescending(x => x.TripCount)
                .Take(limit)
                .ToListAsync();

            return Ok(routeStats);
        }

        /// <summary>
        /// Get travel time trends and patterns
        /// </summary>
        [HttpGet("travel-time-trends/{mapGuid}")]
        public async Task<IActionResult> GetTravelTimeTrends(Guid mapGuid, [FromQuery] int? year)
        {
            var query = GetUserRouteInstances(mapGuid, year)
                .Where(ri => ri.StartTime.HasValue && ri.EndTime.HasValue && ri.DurationHours.HasValue);
            
            var timingData = await query
                .Select(ri => new 
                {
                    Date = ri.Date,
                    DurationHours = ri.DurationHours.Value,
                    StartHour = ri.StartTime.Value.Hour,
                    RouteType = ri.Route.RouteType.Name,
                    RouteTypeNL = ri.Route.RouteType.NameNL,
                    Distance = (double)(ri.Route.OverrideDistance ?? ri.Route.CalculatedDistance)
                })
                .ToListAsync();

            var trends = new
            {
                AverageDurationByMonth = timingData
                    .GroupBy(x => x.Date.ToString("yyyy-MM"))
                    .Select(g => new 
                    {
                        Month = g.Key,
                        AverageDuration = Math.Round(g.Average(x => x.DurationHours), 2),
                        TripCount = g.Count()
                    })
                    .OrderBy(x => x.Month),

                AverageDurationByHour = timingData
                    .GroupBy(x => x.StartHour)
                    .Select(g => new 
                    {
                        Hour = g.Key,
                        AverageDuration = Math.Round(g.Average(x => x.DurationHours), 2),
                        TripCount = g.Count()
                    })
                    .OrderBy(x => x.Hour),

                AverageDurationByRouteType = timingData
                    .GroupBy(x => new { x.RouteType, x.RouteTypeNL })
                    .Select(g => new 
                    {
                        RouteType = g.Key.RouteType,
                        RouteTypeNL = g.Key.RouteTypeNL,
                        AverageDuration = Math.Round(g.Average(x => x.DurationHours), 2),
                        AverageSpeed = g.Any(x => x.Distance > 0) ? Math.Round(g.Where(x => x.Distance > 0).Average(x => x.Distance / x.DurationHours), 2) : 0,
                        TripCount = g.Count()
                    })
                    .OrderByDescending(x => x.TripCount)
            };

            return Ok(trends);
        }

        /// <summary>
        /// Get integration statistics
        /// </summary>
        [HttpGet("integration-stats/{mapGuid}")]
        public async Task<IActionResult> GetIntegrationStats(Guid mapGuid, [FromQuery] int? year)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Forbid();

            var query = GetUserRouteInstances(mapGuid, year);
            
            var totalTrips = await query.CountAsync();
            var tripsWithTiming = await query.Where(ri => ri.StartTime.HasValue && ri.EndTime.HasValue).CountAsync();
            var traewellingImported = await query.Where(ri => ri.TrawellingStatusId.HasValue).CountAsync();
            var tripsWithSource = await query.Where(ri => ri.RouteInstanceProperties.Any(p => p.Key == "source")).CountAsync();

            var sourceBreakdown = await query
                .SelectMany(ri => ri.RouteInstanceProperties.Where(p => p.Key == "source"))
                .GroupBy(p => p.Value)
                .Select(g => new 
                {
                    Source = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            var monthlyImports = await query
                .Where(ri => ri.TrawellingStatusId.HasValue || ri.RouteInstanceProperties.Any(p => p.Key == "source"))
                .GroupBy(ri => ri.Date.ToString("yyyy-MM"))
                .Select(g => new 
                {
                    Month = g.Key,
                    ImportedCount = g.Count()
                })
                .OrderBy(x => x.Month)
                .ToListAsync();

            return Ok(new 
            {
                TotalTrips = totalTrips,
                TripsWithTiming = tripsWithTiming,
                TraewellingImported = traewellingImported,
                TripsWithSource = tripsWithSource,
                SourceBreakdown = sourceBreakdown,
                MonthlyImports = monthlyImports,
                TimingCompleteness = totalTrips > 0 ? Math.Round((double)tripsWithTiming / totalTrips * 100, 1) : 0
            });
        }

        /// <summary>
        /// Get coverage overview with route statistics
        /// </summary>
        [HttpGet("coverage-overview/{mapGuid}")]
        public async Task<IActionResult> GetCoverageOverview(Guid mapGuid, [FromQuery] int? year)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Forbid();

            var query = GetUserRouteInstances(mapGuid, year);
            
            var uniqueRoutes = await query.Select(ri => ri.RouteId).Distinct().CountAsync();
            var totalDistance = await query.SumAsync(ri => (double)(ri.Route.OverrideDistance ?? ri.Route.CalculatedDistance));
            
            var routeTypes = await query
                .GroupBy(ri => new { ri.Route.RouteType.Name, ri.Route.RouteType.NameNL })
                .Select(g => new 
                {
                    RouteType = g.Key.Name,
                    RouteTypeNL = g.Key.NameNL,
                    UniqueRoutes = g.Select(x => x.RouteId).Distinct().Count(),
                    TripCount = g.Count(),
                    TotalDistance = g.Sum(ri => (double)(ri.Route.OverrideDistance ?? ri.Route.CalculatedDistance))
                })
                .OrderByDescending(x => x.TripCount)
                .ToListAsync();

            // Get station coverage (if station visits exist)
            var visitedStations = await _context.StationVisits
                .Where(sv => sv.UserId == userId.Value)
                .CountAsync();

            return Ok(new 
            {
                UniqueRoutes = uniqueRoutes,
                TotalDistance = Math.Round(totalDistance, 2),
                VisitedStations = visitedStations,
                RouteTypeBreakdown = routeTypes,
                CoverageNote = "Coverage statistics are best-effort due to potential route overlaps and partial segments"
            });
        }

        /// <summary>
        /// Test endpoint to verify analytics API is working (no auth required for testing)
        /// </summary>
        [HttpGet("test")]
        [AllowAnonymous]
        public IActionResult TestAnalytics()
        {
            return Ok(new 
            { 
                message = "Enhanced Analytics API is working correctly!",
                timestamp = DateTime.UtcNow,
                version = "1.0.0",
                features = new string[] {
                    "Trip frequency analysis",
                    "GitHub-style activity heatmap", 
                    "Route rankings and usage stats",
                    "Travel time trends",
                    "Integration statistics",
                    "Coverage overview with route overlap handling",
                    "CSV export functionality"
                }
            });
        }
    }
}