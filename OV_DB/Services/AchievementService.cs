using Microsoft.EntityFrameworkCore;
using OV_DB.Models;
using OVDB_database.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OV_DB.Services;

public interface IAchievementService
{
    Task<AchievementsDTO> BuildAsync(Guid map, int userId, CancellationToken cancellationToken = default);
}

public class AchievementService(OVDBDatabaseContext dbContext) : IAchievementService
{
    /// <summary>A value after a given trip. Progressions must be non-decreasing.</summary>
    internal readonly record struct ProgressPoint(DateTime Date, double Value);

    public async Task<AchievementsDTO> BuildAsync(Guid map, int userId, CancellationToken cancellationToken = default)
    {
        var trips = await dbContext.RouteInstances
            .AsNoTracking()
            .Where(ri => ri.Route.RouteMaps.Any(rm => rm.Map.UserId == userId)
                         && (ri.Route.RouteMaps.Any(rm => rm.Map.MapGuid == map)
                             || ri.RouteInstanceMaps.Any(rim => rim.Map.MapGuid == map)))
            .OrderBy(ri => ri.Date)
            .ThenBy(ri => ri.RouteInstanceId)
            .Select(ri => new
            {
                ri.RouteId,
                ri.Date,
                ri.DurationHours,
                ri.StartTime,
                ri.EndTime,
                ri.ScheduledEndTime,
                Operator = ri.Route.OperatingCompany,
                Distance = (double)((ri.Route.OverrideDistance.HasValue && ri.Route.OverrideDistance > 0)
                    ? ri.Route.OverrideDistance
                    : ri.Route.CalculatedDistance)
            })
            .ToListAsync(cancellationToken);

        var result = new AchievementsDTO();
        if (trips.Count == 0)
        {
            return result;
        }

        var routeIds = trips.Select(t => t.RouteId).Distinct().ToList();
        var routeCountries = await dbContext.Routes
            .AsNoTracking()
            .Where(r => routeIds.Contains(r.RouteId))
            .Select(r => new
            {
                r.RouteId,
                IsoCodes = r.Regions.Where(region => region.IsoCode != null && region.IsoCode != "")
                    .Select(region => region.IsoCode)
                    .ToList()
            })
            .ToDictionaryAsync(r => r.RouteId, r => r.IsoCodes, cancellationToken);

        // Running totals, in date order, so each threshold can be dated to the trip that crossed it.
        var distance = new List<ProgressPoint>(trips.Count);
        var tripCount = new List<ProgressPoint>(trips.Count);
        var longestTrip = new List<ProgressPoint>(trips.Count);
        var topSpeed = new List<ProgressPoint>(trips.Count);
        var countries = new List<ProgressPoint>(trips.Count);
        var operators = new List<ProgressPoint>(trips.Count);
        var nightTrains = new List<ProgressPoint>(trips.Count);
        var delayHours = new List<ProgressPoint>(trips.Count);
        var travelDays = new List<ProgressPoint>(trips.Count);

        double totalDistance = 0;
        double maxDistance = 0;
        double maxSpeed = 0;
        double totalDelayHours = 0;
        var nightTrainCount = 0;
        var seenCountries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenOperators = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenDays = new HashSet<DateTime>();

        foreach (var trip in trips)
        {
            totalDistance += trip.Distance;
            distance.Add(new ProgressPoint(trip.Date, Math.Round(totalDistance, 1)));
            tripCount.Add(new ProgressPoint(trip.Date, distance.Count));

            maxDistance = Math.Max(maxDistance, trip.Distance);
            longestTrip.Add(new ProgressPoint(trip.Date, Math.Round(maxDistance, 1)));

            if (trip.DurationHours > 0 && trip.Distance > 0)
            {
                maxSpeed = Math.Max(maxSpeed, trip.Distance / trip.DurationHours.Value);
            }
            topSpeed.Add(new ProgressPoint(trip.Date, Math.Round(maxSpeed, 1)));

            if (routeCountries.TryGetValue(trip.RouteId, out var isoCodes))
            {
                foreach (var iso in isoCodes)
                {
                    seenCountries.Add(iso);
                }
            }
            countries.Add(new ProgressPoint(trip.Date, seenCountries.Count));

            foreach (var name in SplitOperators(trip.Operator))
            {
                seenOperators.Add(name);
            }
            operators.Add(new ProgressPoint(trip.Date, seenOperators.Count));

            // A trip that starts on one calendar day and ends on the next.
            if (trip.StartTime.HasValue && trip.EndTime.HasValue
                && trip.EndTime.Value.Date > trip.StartTime.Value.Date)
            {
                nightTrainCount++;
            }
            nightTrains.Add(new ProgressPoint(trip.Date, nightTrainCount));

            if (trip.EndTime.HasValue && trip.ScheduledEndTime.HasValue)
            {
                var delay = (trip.EndTime.Value - trip.ScheduledEndTime.Value).TotalHours;
                if (delay > 0)
                {
                    totalDelayHours += delay;
                }
            }
            delayHours.Add(new ProgressPoint(trip.Date, Math.Round(totalDelayHours, 2)));

            seenDays.Add(trip.Date.Date);
            travelDays.Add(new ProgressPoint(trip.Date, seenDays.Count));
        }

        result.Families =
        [
            BuildFamily("DISTANCE", "straighten", "km", [1000, 5000, 10000, 25000, 50000, 100000], distance),
            BuildFamily("TRIPS", "confirmation_number", "count", [10, 50, 100, 500, 1000], tripCount),
            BuildFamily("TRAVEL_DAYS", "event_available", "count", [10, 50, 100, 365], travelDays),
            BuildFamily("COUNTRIES", "public", "count", [1, 3, 5, 10, 25], countries),
            BuildFamily("OPERATORS", "badge", "count", [5, 10, 25, 50], operators),
            BuildFamily("LONGEST_TRIP", "trending_flat", "km", [100, 250, 500, 1000], longestTrip),
            BuildFamily("TOP_SPEED", "speed", "kmh", [80, 100, 160, 200], topSpeed),
            BuildFamily("NIGHT_TRAINS", "bedtime", "count", [1, 5, 10, 25], nightTrains),
            BuildFamily("DELAY_TIME", "hourglass_bottom", "hours", [1, 5, 24, 72], delayHours),
        ];

        return result;
    }

    /// <summary>
    /// Multi-operator routes store a comma separated list, matching how the Trainlog export
    /// splits them.
    /// </summary>
    internal static IEnumerable<string> SplitOperators(string operatingCompany)
    {
        if (string.IsNullOrWhiteSpace(operatingCompany))
        {
            yield break;
        }
        foreach (var part in operatingCompany.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0)
            {
                yield return trimmed;
            }
        }
    }

    /// <summary>
    /// Turns a non-decreasing progression into tiers. Each threshold is dated to the first trip
    /// at which the running value reached it, so "earned on" needs no stored state.
    /// </summary>
    internal static AchievementFamilyDTO BuildFamily(string key, string icon, string unit, IReadOnlyList<double> thresholds, IReadOnlyList<ProgressPoint> progression)
    {
        var currentValue = progression.Count > 0 ? progression[^1].Value : 0;

        var tiers = new List<AchievementTierDTO>(thresholds.Count);
        for (var index = 0; index < thresholds.Count; index++)
        {
            var threshold = thresholds[index];
            DateTime? earnedOn = null;
            foreach (var point in progression)
            {
                if (point.Value >= threshold)
                {
                    earnedOn = point.Date;
                    break;
                }
            }
            tiers.Add(new AchievementTierDTO
            {
                Tier = index + 1,
                Threshold = threshold,
                Earned = currentValue >= threshold,
                EarnedOn = earnedOn
            });
        }

        var earned = tiers.Where(t => t.Earned).ToList();
        var next = tiers.FirstOrDefault(t => !t.Earned);
        var previousThreshold = earned.Count > 0 ? earned[^1].Threshold : 0;

        double progress = 1;
        if (next != null)
        {
            var span = next.Threshold - previousThreshold;
            progress = span <= 0 ? 0 : Math.Clamp((currentValue - previousThreshold) / span, 0, 1);
        }

        return new AchievementFamilyDTO
        {
            Key = key,
            Icon = icon,
            Unit = unit,
            CurrentValue = currentValue,
            EarnedTiers = earned.Count,
            TotalTiers = tiers.Count,
            CurrentTier = earned.Count > 0 ? earned[^1] : null,
            NextTier = next,
            ProgressToNext = Math.Round(progress, 4)
        };
    }
}
