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
    /// Acting on the station suggestions an import produced.
    /// </summary>
    /// <remarks>
    /// The suggestions themselves are made at import — see <see cref="IStationSuggestionService"/> —
    /// because only the importer has the operator's calling pattern, and route geometry cannot tell
    /// stopping from passing through. This controller is only the two things the user can do with
    /// one: mark it, or say stop asking. Both are per station; there is no "accept all".
    /// </remarks>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class StationSuggestionsController(
        OVDBDatabaseContext context,
        IStationSuggestionService suggestionService,
        IStationVisitService stationVisitService) : ControllerBase
    {
        /// <summary>
        /// Stations an OSM relation calls at that are not marked visited, from the stops the
        /// importer parsed out of the relation's members.
        /// </summary>
        /// <remarks>
        /// Called once, straight after importing a route, with the stops that import already
        /// produced — no extra OSM request. Unlike the Träwelling path these suggestions carry no
        /// date, because a freshly imported route has no trip on it yet; marking one leaves an
        /// undated visit, which is exactly what the backfill is for.
        /// </remarks>
        [HttpPost("from-stops")]
        public async Task<IActionResult> FromStops([FromBody] List<OSMStopDTO> stops)
        {
            var userId = User.GetUserId();
            if (userId < 0)
            {
                return Forbid();
            }
            if (stops == null || stops.Count == 0)
            {
                return Ok(new List<StationSuggestionDTO>());
            }

            var points = stops.Select(s => new StopPoint(s.Name, s.Lattitude, s.Longitude));
            return Ok(await suggestionService.FromStopsAsync(userId, points));
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
    }
}
