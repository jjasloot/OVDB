using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Simplify;
using OV_DB.Models;
using OV_DB.Services;
using OVDB_database.Database;

namespace OV_DB.Controllers
{
    /// <summary>
    /// Dating existing visits, one station at a time.
    /// </summary>
    /// <remarks>
    /// This controller <b>cannot create a visit</b>. Its queue is drawn from visits that already
    /// exist, and its only write is <see cref="IStationVisitService.SkipDatingAsync"/>; dating itself
    /// goes through <c>PUT /api/station/{id}/dates</c>, which also refuses to create one. The ~2,900
    /// stations this user has passed but not marked never appear here.
    /// </remarks>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class StationBackfillController(
        OVDBDatabaseContext context,
        IStationTripMatcher matcher,
        IStationVisitService stationVisitService) : ControllerBase
    {
        /// <summary>
        /// The next undated visit to work on. <paramref name="skip"/> steps past ones the user has
        /// looked at and left alone, without recording anything about them.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetNext([FromQuery] int skip = 0)
        {
            var userId = User.GetUserId();
            if (userId < 0)
            {
                return Forbid();
            }

            var queue = Queue(userId);
            var remaining = await queue.CountAsync();

            var station = await queue
                // Geographic order, so consecutive stations tend to share the journeys that explain
                // them and the user stays in one mental place instead of hopping the map.
                .OrderBy(sv => sv.Station.StationCountryId)
                .ThenBy(sv => sv.Station.Name)
                .Skip(skip)
                .Select(sv => new
                {
                    sv.StationId,
                    sv.Station.Name,
                    sv.Station.Lattitude,
                    sv.Station.Longitude,
                    Regions = sv.Station.Regions.Select(r => r.OriginalName)
                })
                .FirstOrDefaultAsync();

            if (station == null)
            {
                return Ok(new StationBackfillItemDTO { Remaining = remaining, Candidates = [] });
            }

            var candidates = await matcher.FindTripsForStationAsync(userId, station.StationId);
            var groups = TripCandidateGrouping.Group(candidates);

            // The earliest candidate, full stop. Groups arrive in chronological order, and the
            // question this screen asks is which trip brought you here first — so pre-selecting a
            // later terminating service over an earlier passing one would answer a different
            // question. Where the earliest only passes and a later one terminates here, the screen
            // leads with "stopped then, got off later" instead, which is the honest reading.
            var suggested = groups.FirstOrDefault();

            return Ok(new StationBackfillItemDTO
            {
                Remaining = remaining,
                StationId = station.StationId,
                StationName = station.Name,
                Lattitude = station.Lattitude,
                Longitude = station.Longitude,
                Regions = station.Regions,
                Candidates = groups,
                SuggestedRouteInstanceId = suggested?.Instances[0].RouteInstanceId,
                SuggestionIsEndpoint = suggested?.IsEndpoint ?? false
            });
        }

        /// <summary>
        /// Retires a station from the queue without asserting anything about it. Deliberately not a
        /// denial — this flow never asks whether the station was visited, only when.
        /// </summary>
        [HttpPost("{stationId:int}/skip")]
        public async Task<IActionResult> Skip(int stationId)
        {
            var userId = User.GetUserId();
            if (userId < 0)
            {
                return Forbid();
            }

            return await stationVisitService.SkipDatingAsync(userId, stationId) ? Ok() : NotFound();
        }

        /// <summary>
        /// The selected route's line, to draw under the station. Seeing the line sweep through is
        /// the evidence; a lone pin is not.
        /// </summary>
        [HttpGet("route/{routeId:int}")]
        public async Task<IActionResult> GetRouteGeometry(int routeId)
        {
            var userId = User.GetUserId();
            if (userId < 0)
            {
                return Forbid();
            }

            var line = await context.Routes.AsNoTracking()
                .Where(r => r.RouteId == routeId)
                .Where(r => r.RouteMaps.Any(rm => rm.Map.UserId == userId))
                .Select(r => r.LineString)
                .SingleOrDefaultAsync();
            if (line == null)
            {
                return NotFound();
            }

            // Drawn at map scale, so display tolerance rather than the matcher's: a route can carry
            // 17,000 coordinates and none of them are visible.
            var simplified = DouglasPeuckerSimplifier.Simplify(line, 100.0 / 111_320.0);

            return Ok(new RouteGeometryDTO
            {
                RouteId = routeId,
                Coordinates = simplified.Coordinates.Select(c => new[] { c.Y, c.X }).ToList()
            });
        }

        /// <summary>
        /// Undated visits, not skipped, on stations the map still shows. An entry/exit date always
        /// implies a stopped date, so a null stopped date means the visit carries no date at all.
        /// </summary>
        private IQueryable<OVDB_database.Models.StationVisit> Queue(int userId) =>
            context.StationVisits.AsNoTracking()
                .Where(sv => sv.UserId == userId)
                .Where(sv => sv.FirstStoppedDate == null)
                .Where(sv => !sv.DatingSkipped)
                .Where(sv => !sv.Station.Hidden && !sv.Station.Special);
    }
}
