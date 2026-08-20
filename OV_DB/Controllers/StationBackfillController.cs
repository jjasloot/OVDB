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
        /// How tall a band of the sweep is, in degrees of latitude — about 28 km.
        /// </summary>
        /// <remarks>
        /// Measured over the real 4,970-station queue, as the distance from one station to the next:
        /// <list type="bullet">
        /// <item>by country then name, as this used to be: median 72.5 km, mean 128.2 km, 2,925 hops
        /// over 50 km</item>
        /// <item>by country then latitude: median 19.1 km, mean 54.5 km, 1,583 over 50 km</item>
        /// <item>in bands of this size, snaking: <b>median 5.1 km</b>, mean 14.8 km, 210 over 50 km,
        /// and half of all hops under 5 km</item>
        /// </list>
        /// Narrower bands lower the median a little further (4.3 km at 0.1°) but raise the mean, because
        /// each band holds fewer stations and the jump between bands comes round more often.
        /// <para>
        /// This is not only tidier. Adjacent stations tend to sit on the same line, so the trips that
        /// explain one tend to explain the next — the answer stays in the same memory instead of being
        /// rebuilt from scratch — and the map pans a few kilometres rather than teleporting, which keeps
        /// the line that was just being judged on screen.
        /// </para>
        /// </remarks>
        private const double BandDegrees = 0.25;

        /// <summary>
        /// The next undated visit to work on. <paramref name="skip"/> steps past ones the user has
        /// looked at and left alone, without recording anything about them.
        /// <paramref name="stationId"/> asks for one particular station instead, which is how undo
        /// returns to the answer it just took back.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetNext([FromQuery] int skip = 0, [FromQuery] int? stationId = null)
        {
            var userId = User.GetUserId();
            if (userId < 0)
            {
                return Forbid();
            }

            var queue = Queue(userId);
            var remaining = await queue.CountAsync();

            var ordered = queue
                // A sweep across the map rather than down the alphabet: north to south in bands, and
                // along each band, reversing direction at every band so the end of one meets the start
                // of the next. See BandDegrees for what this is worth.
                .OrderBy(sv => sv.Station.StationCountryId)
                .ThenByDescending(sv => Math.Floor(sv.Station.Lattitude / BandDegrees))
                .ThenBy(sv =>
                    Math.Floor(sv.Station.Lattitude / BandDegrees) % 2 == 0
                        ? sv.Station.Longitude
                        : -sv.Station.Longitude)
                // Two stations at one place, which happens, would otherwise come back in an order the
                // database is free to change between requests.
                .ThenBy(sv => sv.Station.Name)
                .Select(sv => new
                {
                    sv.StationId,
                    sv.Station.Name,
                    sv.Station.Lattitude,
                    sv.Station.Longitude,
                    Regions = sv.Station.Regions.Select(r => r.OriginalName)
                });

            // Undo names the station it just put back rather than a position in the queue: the queue
            // has grown by one at that point, so the position the user was at no longer points at the
            // station they were looking at.
            var station = stationId.HasValue
                ? await ordered.FirstOrDefaultAsync(s => s.StationId == stationId.Value)
                : await ordered.Skip(skip).FirstOrDefaultAsync();

            // Asked for by id, it may have left the queue in the meantime - answered again in another
            // tab, say - so fall back to the queue instead of claiming the work is finished.
            if (station == null && stationId.HasValue)
            {
                station = await ordered.Skip(skip).FirstOrDefaultAsync();
            }

            if (station == null)
            {
                return Ok(new StationBackfillItemDTO { Remaining = remaining, Candidates = [], NonTrainCandidates = [] });
            }

            var trips = await matcher.FindStationTripsAsync(userId, station.StationId);
            var groups = TripCandidateGrouping.Group(trips.Offered);

            // The earliest candidate, except that among that first day's trips one starting or
            // ending here wins. See TripCandidateGrouping.Preselect for why the exception stops at
            // the day boundary.
            var suggested = TripCandidateGrouping.Preselect(groups);

            return Ok(new StationBackfillItemDTO
            {
                SuggestedRouteGeometry = suggested == null ? null : await GeometryAsync(userId, suggested.RouteId),
                Remaining = remaining,
                StationId = station.StationId,
                StationName = station.Name,
                Lattitude = station.Lattitude,
                Longitude = station.Longitude,
                Regions = station.Regions,
                Candidates = groups,
                // Sent with the station rather than fetched when asked for: it is the same search
                // either way, so a second round trip would buy nothing, and the count is needed up
                // front to know whether to offer the button at all.
                NonTrainCandidates = TripCandidateGrouping.Group(trips.Withheld),
                SuggestedRouteInstanceId = suggested?.Instances[0].RouteInstanceId,
                SuggestionIsEndpoint = suggested?.IsEndpoint ?? false,
                RegionProgress = await RegionProgressAsync(userId, station.StationId)
            });
        }

        /// <summary>
        /// Dating progress for the region the given station sits in, as something finishable to set
        /// against a five-thousand-station queue.
        /// </summary>
        /// <remarks>
        /// The level taken is the one directly under the country — provinces, Bundesländer, cantons —
        /// which is both the finishable unit and the level the region achievements already count.
        /// </remarks>
        private async Task<RegionProgressDTO> RegionProgressAsync(int userId, int stationId)
        {
            var region = await context.Regions.AsNoTracking()
                .Where(r => r.ParentRegionId != null && r.ParentRegion.ParentRegionId == null)
                .Where(r => r.Stations.Any(s => s.Id == stationId))
                .Select(r => new { r.Id, r.OriginalName })
                .FirstOrDefaultAsync();

            if (region == null)
            {
                return null;
            }

            // Counted over visits rather than stations: the queue is drawn from visits, so the total has
            // to be the ones the user has actually marked, not every station in the province.
            var counts = await context.StationVisits.AsNoTracking()
                .Where(sv => sv.UserId == userId)
                .Where(sv => !sv.Station.Hidden && !sv.Station.Special)
                .Where(sv => sv.Station.Regions.Any(r => r.Id == region.Id))
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Dated = g.Count(sv => sv.FirstStoppedDate != null)
                })
                .FirstOrDefaultAsync();

            return counts == null
                ? null
                : new RegionProgressDTO
                {
                    Name = region.OriginalName,
                    Dated = counts.Dated,
                    Total = counts.Total
                };
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

            var geometry = await GeometryAsync(userId, routeId);
            return geometry == null ? NotFound() : Ok(geometry);
        }

        /// <summary>
        /// A route's line, unsimplified. This is one route on a map the user zooms into to judge
        /// whether it really runs through the platform, and a smoothed line is the wrong thing to
        /// make that call on. One route's worth of coordinates is a cheap payload.
        /// </summary>
        private async Task<RouteGeometryDTO> GeometryAsync(int userId, int routeId)
        {
            var line = await context.Routes.AsNoTracking()
                .Where(r => r.RouteId == routeId)
                .Where(r => r.RouteMaps.Any(rm => rm.Map.UserId == userId))
                .Select(r => r.LineString)
                .SingleOrDefaultAsync();

            return line == null
                ? null
                : new RouteGeometryDTO
                {
                    RouteId = routeId,
                    Coordinates = line.Coordinates.Select(c => new[] { c.Y, c.X }).ToList()
                };
        }

        /// <summary>
        /// Puts a station the user set aside back in the dating queue — the undo for "can't
        /// remember". Dating itself is undone by clearing the dates, which needs nothing new.
        /// </summary>
        [HttpDelete("{stationId:int}/skip")]
        public async Task<IActionResult> Unskip(int stationId)
        {
            var userId = User.GetUserId();
            if (userId < 0)
            {
                return Forbid();
            }

            return await stationVisitService.ResumeDatingAsync(userId, stationId) ? Ok() : NotFound();
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
