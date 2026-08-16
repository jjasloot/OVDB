using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OV_DB.Models;
using OV_DB.Services;
using OVDB_database.Database;
using OVDB_database.Enums;
using OVDB_database.Models;

namespace OV_DB.Controllers
{
    /// <summary>
    /// Stations a trip passes that are not marked visited.
    /// </summary>
    /// <remarks>
    /// This is the only flow that turns inference into visits, and it does so strictly one tick at a
    /// time: nothing here marks anything until the user asks for a specific station, and there is no
    /// "accept all". Proximity is only ~66% precise — a measured 14% of unvisited stations sit within
    /// 300 m of a ridden route — which is exactly why it proposes and never decides.
    /// </remarks>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class StationSuggestionsController(
        OVDBDatabaseContext context,
        IStationTripMatcher matcher,
        IStationVisitService stationVisitService) : ControllerBase
    {
        /// <summary>
        /// How far back to look. Measured: the 25 most recent trips yielded nothing at all for this
        /// user, while 250 found 38 trips with suggestions — they cluster in a travel period rather
        /// than at the top of the list, so a small window simply misses them.
        /// </summary>
        private const int MaxTripsToScan = 250;

        /// <summary>
        /// Stop once this many trips have something to offer. Scanning all 250 costs ~3 s; stopping
        /// early usually costs far less, and twenty trips is already more than one sitting's work.
        /// </summary>
        private const int MaxTripsToReturn = 20;

        /// <summary>
        /// Recent trips that pass unmarked stations. Träwelling check-ins arrive in the background,
        /// so there is no import moment to interrupt — this is the list to come back to instead.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetRecent()
        {
            var userId = User.GetUserId();
            if (userId < 0)
            {
                return Forbid();
            }

            var trips = await context.RouteInstances.AsNoTracking()
                .Where(ri => ri.Route.RouteMaps.Any(rm => rm.Map.UserId == userId))
                .OrderByDescending(ri => ri.Date)
                .Take(MaxTripsToScan)
                .Select(ri => new
                {
                    ri.RouteInstanceId,
                    ri.RouteId,
                    ri.Date,
                    ri.Route.Name,
                    ri.Route.From,
                    ri.Route.To
                })
                .ToListAsync();

            var results = new List<TripSuggestionsDTO>();
            foreach (var trip in trips)
            {
                var stations = await SuggestionsForAsync(userId, trip.RouteInstanceId);
                if (stations.Count == 0)
                {
                    continue;
                }
                results.Add(new TripSuggestionsDTO
                {
                    RouteInstanceId = trip.RouteInstanceId,
                    RouteId = trip.RouteId,
                    RouteName = trip.Name,
                    From = trip.From,
                    To = trip.To,
                    Date = trip.Date,
                    Stations = stations
                });

                if (results.Count >= MaxTripsToReturn)
                {
                    break;
                }
            }

            return Ok(results);
        }

        /// <summary>Suggestions for one trip, for coming back to a specific journey.</summary>
        [HttpGet("{routeInstanceId:int}")]
        public async Task<IActionResult> GetForTrip(int routeInstanceId)
        {
            var userId = User.GetUserId();
            if (userId < 0)
            {
                return Forbid();
            }

            return Ok(await SuggestionsForAsync(userId, routeInstanceId));
        }

        /// <summary>
        /// Marks one suggested station, dated from the trip that suggested it. The tick is the
        /// explicit user action the base requirement demands; the trip only supplies the date.
        /// </summary>
        [HttpPost("mark")]
        public async Task<IActionResult> Mark([FromBody] MarkSuggestionDTO suggestion)
        {
            var userId = User.GetUserId();
            if (userId < 0)
            {
                return Forbid();
            }

            try
            {
                await stationVisitService.MarkFromTripAsync(
                    userId, suggestion.StationId, suggestion.Level, suggestion.RouteInstanceId, StationVisitSource.ImportSuggested);
            }
            catch (ArgumentException)
            {
                return BadRequest();
            }

            return Ok();
        }

        /// <summary>
        /// Stops a station being suggested again. Says nothing about whether it was visited — only
        /// that the user does not want to be asked, which is why it is its own table rather than a
        /// flag on a visit that does not exist.
        /// </summary>
        [HttpPost("{stationId:int}/dismiss")]
        public async Task<IActionResult> Dismiss(int stationId)
        {
            var userId = User.GetUserId();
            if (userId < 0)
            {
                return Forbid();
            }

            var alreadyDismissed = await context.StationSuggestionDismissals
                .AnyAsync(d => d.UserId == userId && d.StationId == stationId);
            if (!alreadyDismissed)
            {
                context.StationSuggestionDismissals.Add(new StationSuggestionDismissal
                {
                    UserId = userId,
                    StationId = stationId,
                    DismissedOn = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
            }

            return Ok();
        }

        private async Task<List<StationSuggestionDTO>> SuggestionsForAsync(int userId, int routeInstanceId)
        {
            var candidates = await matcher.FindStationsForTripAsync(userId, routeInstanceId);
            if (candidates.Count == 0)
            {
                return [];
            }

            var stationIds = candidates.Select(c => c.StationId).ToList();

            // Anything already marked, or explicitly dismissed, is not a suggestion.
            var visited = await context.StationVisits.AsNoTracking()
                .Where(sv => sv.UserId == userId && stationIds.Contains(sv.StationId))
                .Select(sv => sv.StationId)
                .ToListAsync();
            var dismissed = await context.StationSuggestionDismissals.AsNoTracking()
                .Where(d => d.UserId == userId && stationIds.Contains(d.StationId))
                .Select(d => d.StationId)
                .ToListAsync();
            var excluded = visited.Concat(dismissed).ToHashSet();

            var positions = await context.Stations.AsNoTracking()
                .Where(s => stationIds.Contains(s.Id))
                .Select(s => new { s.Id, s.Lattitude, s.Longitude })
                .ToDictionaryAsync(s => s.Id);

            return candidates
                .Where(c => !excluded.Contains(c.StationId))
                .Select(c => new StationSuggestionDTO
                {
                    StationId = c.StationId,
                    StationName = c.StationName,
                    Lattitude = positions[c.StationId].Lattitude,
                    Longitude = positions[c.StationId].Longitude,
                    IsEndpoint = c.Evidence == VisitEvidence.RouteEndpoint,
                    DistanceMetres = c.DistanceMetres
                })
                .ToList();
        }
    }
}
