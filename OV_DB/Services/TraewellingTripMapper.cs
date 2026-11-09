using Microsoft.Extensions.Logging;
using OV_DB.Models;
using OVDB_database.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Services
{
    /// <summary>
    /// Turns Träwelling statuses into OVDB's trip DTOs (station-local times), and the pure
    /// comparisons the inbox uses to decide whether an upstream edit is a real change.
    /// </summary>
    public class TraewellingTripMapper(ITimezoneService _timezoneService, ILogger<TraewellingTripMapper> _logger)
    {
        internal static DateTime? GetStatusDeparture(TrawellingStatus status)
        {
            var departure = status.Checkin?.ManualDeparture
                ?? status.Checkin?.Origin?.DepartureReal
                ?? status.Checkin?.Origin?.DeparturePlanned;
            return departure?.UtcDateTime;
        }

        /// <summary>
        /// The journey facts a conflict flag is about: line and the stations/times of both
        /// ends. Social fields (likes, tags, body) deliberately don't count — a change there
        /// must not re-flag a dismissed conflict.
        /// </summary>
        internal static string ConflictFingerprint(TrawellingStatus status)
        {
            var checkin = status?.Checkin;
            return string.Join("|",
                checkin?.LineName,
                checkin?.Origin?.Station?.Name,
                checkin?.Destination?.Station?.Name,
                checkin?.ManualDeparture?.UtcDateTime.ToString("o"),
                checkin?.Origin?.DeparturePlanned?.UtcDateTime.ToString("o"),
                checkin?.Origin?.DepartureReal?.UtcDateTime.ToString("o"),
                checkin?.ManualArrival?.UtcDateTime.ToString("o"),
                checkin?.Destination?.ArrivalPlanned?.UtcDateTime.ToString("o"),
                checkin?.Destination?.ArrivalReal?.UtcDateTime.ToString("o"));
        }

        /// <summary>
        /// Whether the upstream status still says something different from the RouteInstance that
        /// was imported from it. Träwelling fires <c>checkin_update</c> for likes, tags, body edits
        /// and visibility changes as well as for journey edits, and a notice about a trip whose
        /// four comparable values all match asks the user to spot a difference that isn't there.
        /// </summary>
        /// <remarks>
        /// Deliberately the same four comparisons, with the same tolerances, that the conflict card
        /// renders: if the card cannot show a difference, there is no conflict to raise. A status
        /// that cannot be mapped counts as different — losing a real upstream edit is the worse
        /// failure of the two.
        /// </remarks>
        internal static bool UpstreamDiffers(RouteInstance instance, TrawellingTripDto newTrip)
            => UpstreamDiffers(instance.Route?.From, instance.Route?.To, instance.StartTime, instance.EndTime, newTrip);

        /// <summary>The same check against a conflict already built for the UI.</summary>
        internal static bool UpstreamDiffers(TrawellingConflictDto conflict)
            => UpstreamDiffers(conflict.RouteFrom, conflict.RouteTo, conflict.InstanceStartTime, conflict.InstanceEndTime, conflict.NewTrip);

        private static bool UpstreamDiffers(string currentFrom, string currentTo, DateTime? currentStart, DateTime? currentEnd, TrawellingTripDto newTrip)
        {
            var transport = newTrip?.Transport;
            if (transport == null)
            {
                return true;
            }

            return StationsDiffer(currentFrom, transport.Origin?.Name)
                || StationsDiffer(currentTo, transport.Destination?.Name)
                || TimesDiffer(currentStart, transport.Origin?.DepartureReal ?? transport.Origin?.DepartureScheduled)
                || TimesDiffer(currentEnd, transport.Destination?.ArrivalReal ?? transport.Destination?.ArrivalScheduled);
        }

        /// <summary>Minute precision: sub-minute serialisation noise is not a change.</summary>
        private static bool TimesDiffer(DateTime? current, DateTime? upstream)
        {
            if (!current.HasValue || !upstream.HasValue)
            {
                return current.HasValue != upstream.HasValue;
            }
            return current.Value.Ticks / TimeSpan.TicksPerMinute != upstream.Value.Ticks / TimeSpan.TicksPerMinute;
        }

        /// <summary>
        /// OVDB route endpoints are user-editable text, so only a clear difference between two
        /// known names counts — an endpoint the user renamed is not an upstream edit.
        /// </summary>
        private static bool StationsDiffer(string current, string upstream)
        {
            if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(upstream))
            {
                return false;
            }
            static string Normalise(string value) => string.Join(' ',
                value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
            return Normalise(current) != Normalise(upstream);
        }

        public async Task<TrawellingTripDto> MapStatusToTripDtoAsync(TrawellingStatus status)
        {
            try
            {
                var transport = status.Checkin;
                if (transport?.Origin?.Station == null || transport?.Destination?.Station == null)
                    return null;

                var origin = await MapStopoverToDto(transport.Origin, transport.ManualDeparture, isArrival: false);
                var destination = await MapStopoverToDto(transport.Destination, transport.ManualArrival, isArrival: true);

                return new TrawellingTripDto
                {
                    Id = status.Id,
                    Body = status.Body,
                    Business = status.Business,
                    Visibility = status.Visibility,
                    CreatedAt = status.CreatedAt,
                    Transport = new TrawellingTransportDto
                    {
                        Category = transport.Category,
                        Number = transport.Number,
                        LineName = transport.LineName,
                        JourneyNumber = !string.IsNullOrWhiteSpace(transport.ManualJourneyNumber) ? transport.ManualJourneyNumber : transport.JourneyNumber.ToString(),
                        Distance = transport.Distance,
                        Duration = transport.Duration,
                        Origin = origin,
                        Destination = destination,
                        Operator = transport.Operator != null ? new TrawellingOperatorDto
                        {
                            Name = transport.Operator.Name
                        } : null
                    },
                    UserDetails = status.User != null ? new TrawellingLightUserDto
                    {
                        Id = status.User.Id,
                        DisplayName = status.User.DisplayName,
                        Username = status.User.Username,
                        ProfilePicture = status.User.ProfilePicture
                    } : null,
                    Tags = status.Tags?.Select(t => new TrawellingStatusTagDto
                    {
                        Key = t.Key,
                        Value = t.Value,
                        Visibility = t.Visibility
                    }).ToList() ?? new List<TrawellingStatusTagDto>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapping status {StatusId} to trip DTO", status.Id);
                return null;
            }
        }

        private async Task<TrawellingStopoverDto> MapStopoverToDto(TrawellingStopover stopover, DateTimeOffset? manualTime, bool isArrival)
        {
            var station = stopover.Station;
            try
            {
                // Determine the best real times (manual times take precedence)
                var realArrivalAt = isArrival ? manualTime ?? stopover.ArrivalReal ?? stopover.ArrivalPlanned : null;
                var realDepartureAt = isArrival ? null : manualTime ?? stopover.DepartureReal ?? stopover.DeparturePlanned;
                DateTime? realArrival = realArrivalAt?.UtcDateTime;
                DateTime? realDeparture = realDepartureAt?.UtcDateTime;

                // Convert UTC times to local timezone if we have coordinates
                DateTime? localArrivalScheduled = null;
                DateTime? localDepartureScheduled = null;
                DateTime? localArrivalReal = null;
                DateTime? localDepartureReal = null;

                if (station.Latitude.HasValue && station.Longitude.HasValue)
                {
                    if (stopover.ArrivalPlanned.HasValue)
                        localArrivalScheduled = await _timezoneService.ConvertUtcToLocalTimeAsync(stopover.ArrivalPlanned.Value.UtcDateTime, station.Latitude.Value, station.Longitude.Value);

                    if (stopover.DeparturePlanned.HasValue)
                        localDepartureScheduled = await _timezoneService.ConvertUtcToLocalTimeAsync(stopover.DeparturePlanned.Value.UtcDateTime, station.Latitude.Value, station.Longitude.Value);

                    if (realArrival.HasValue)
                        localArrivalReal = await _timezoneService.ConvertUtcToLocalTimeAsync(realArrival.Value, station.Latitude.Value, station.Longitude.Value);

                    if (realDeparture.HasValue)
                        localDepartureReal = await _timezoneService.ConvertUtcToLocalTimeAsync(realDeparture.Value, station.Latitude.Value, station.Longitude.Value);
                }
                else
                {
                    // No coordinates to look the zone up with: Träwelling's own offset gives the
                    // station's clock time, which is what every other stopover stores.
                    localArrivalScheduled = stopover.ArrivalPlanned?.DateTime;
                    localDepartureScheduled = stopover.DeparturePlanned?.DateTime;
                    localArrivalReal = realArrivalAt?.DateTime;
                    localDepartureReal = realDepartureAt?.DateTime;

                    _logger.LogWarning("No coordinates available for station {StationName}, using Träwelling's offsets", station.Name);
                }

                return new TrawellingStopoverDto
                {
                    Name = station.Name,
                    ArrivalScheduled = localArrivalScheduled,
                    DepartureScheduled = localDepartureScheduled,
                    ArrivalPlatformPlanned = stopover.ArrivalPlatformPlanned,
                    DeparturePlatformPlanned = stopover.DeparturePlatformPlanned,
                    ArrivalReal = localArrivalReal,
                    DepartureReal = localDepartureReal,
                    ArrivalPlatformReal = stopover.ArrivalPlatformReal,
                    DeparturePlatformReal = stopover.DeparturePlatformReal,
                    IsArrivalDelayed = stopover.IsArrivalDelayed,
                    IsDepartureDelayed = stopover.IsDepartureDelayed,
                    Cancelled = stopover.Cancelled,
                    Platform = stopover.Platform
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapping stopover at {StationName} to DTO", station?.Name);
                // Return basic DTO without timezone conversion on error
                return new TrawellingStopoverDto
                {
                    Name = station?.Name,
                    ArrivalScheduled = stopover.ArrivalPlanned?.DateTime,
                    DepartureScheduled = stopover.DeparturePlanned?.DateTime,
                    ArrivalReal = (stopover.ArrivalReal ?? stopover.ArrivalPlanned)?.DateTime,
                    DepartureReal = (stopover.DepartureReal ?? stopover.DeparturePlanned)?.DateTime,
                    IsArrivalDelayed = stopover.IsArrivalDelayed,
                    IsDepartureDelayed = stopover.IsDepartureDelayed,
                    Cancelled = stopover.Cancelled
                };
            }
        }
    }
}
