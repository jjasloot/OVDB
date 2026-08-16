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

    /// <summary>Country groups worth collecting, by ISO code.</summary>
    private static readonly (string Key, string Icon, string[] Iso)[] CountryGroups =
    [
        ("BENELUX", "handshake", ["NL", "BE", "LU"]),
        ("DACH", "landscape", ["DE", "AT", "CH"]),
        ("NORDICS", "ac_unit", ["SE", "NO", "DK", "FI"]),
    ];

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

        var routeRegions = await dbContext.Routes
            .AsNoTracking()
            .Where(r => routeIds.Contains(r.RouteId))
            .Select(r => new
            {
                r.RouteId,
                IsoCodes = r.Regions.Where(region => region.IsoCode != null && region.IsoCode != "")
                    .Select(region => region.IsoCode)
                    .ToList(),
                RegionIds = r.Regions.Select(region => region.Id).ToList(),
                OperatorIds = r.Operators.Select(o => o.Id).ToList()
            })
            .ToDictionaryAsync(r => r.RouteId, r => r, cancellationToken);

        // The whole region tree: which level is worth collecting differs per country (Dutch
        // provinces sit directly under the country, Belgian ones sit under its three regions).
        var allRegions = await dbContext.Regions
            .AsNoTracking()
            .Select(r => new RegionNode(r.Id, r.ParentRegionId, r.Name, r.NameNL, r.IsoCode))
            .ToListAsync(cancellationToken);

        var (subdivisionParent, countryInfo) = ResolveCollectibleRegions(allRegions);

        var deutscheBahnOperatorIds = await FindDeutscheBahnOperatorIdsAsync(cancellationToken);

        var distance = new List<ProgressPoint>(trips.Count);
        var tripCount = new List<ProgressPoint>(trips.Count);
        var longestTrip = new List<ProgressPoint>(trips.Count);
        var topSpeed = new List<ProgressPoint>(trips.Count);
        var countries = new List<ProgressPoint>(trips.Count);
        var operatorsSeen = new List<ProgressPoint>(trips.Count);
        var nightTrains = new List<ProgressPoint>(trips.Count);
        var delayHours = new List<ProgressPoint>(trips.Count);
        var travelDays = new List<ProgressPoint>(trips.Count);
        var borderHops = new List<ProgressPoint>(trips.Count);
        var deutscheBahned = new List<ProgressPoint>(trips.Count);
        var shortHops = new List<ProgressPoint>(trips.Count);
        var countryGroupProgress = CountryGroups.ToDictionary(g => g.Key, _ => new List<ProgressPoint>(trips.Count));

        double totalDistance = 0;
        double maxDistance = 0;
        double maxSpeed = 0;
        double totalDelayHours = 0;
        double worstDbDelay = 0;
        var nightTrainCount = 0;
        var borderHopCount = 0;
        var shortHopCount = 0;
        var seenCountries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenOperators = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenDays = new HashSet<DateTime>();

        foreach (var trip in trips)
        {
            routeRegions.TryGetValue(trip.RouteId, out var route);

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

            var tripIsoCodes = route?.IsoCodes ?? [];
            foreach (var iso in tripIsoCodes)
            {
                seenCountries.Add(iso);
            }
            countries.Add(new ProgressPoint(trip.Date, seenCountries.Count));

            // A single journey that touches more than one country.
            if (tripIsoCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            {
                borderHopCount++;
            }
            borderHops.Add(new ProgressPoint(trip.Date, borderHopCount));

            foreach (var group in CountryGroups)
            {
                var have = group.Iso.Count(iso => seenCountries.Contains(iso));
                countryGroupProgress[group.Key].Add(new ProgressPoint(trip.Date, have));
            }

            foreach (var name in SplitOperators(trip.Operator))
            {
                seenOperators.Add(name);
            }
            operatorsSeen.Add(new ProgressPoint(trip.Date, seenOperators.Count));

            if (trip.StartTime.HasValue && trip.EndTime.HasValue
                && trip.EndTime.Value.Date > trip.StartTime.Value.Date)
            {
                nightTrainCount++;
            }
            nightTrains.Add(new ProgressPoint(trip.Date, nightTrainCount));

            double arrivalDelay = 0;
            if (trip.EndTime.HasValue && trip.ScheduledEndTime.HasValue)
            {
                arrivalDelay = (trip.EndTime.Value - trip.ScheduledEndTime.Value).TotalMinutes;
                if (arrivalDelay > 0)
                {
                    totalDelayHours += arrivalDelay / 60.0;
                }
            }
            delayHours.Add(new ProgressPoint(trip.Date, Math.Round(totalDelayHours, 2)));

            if (arrivalDelay > 0 && IsDeutscheBahn(route?.OperatorIds, trip.Operator, deutscheBahnOperatorIds))
            {
                worstDbDelay = Math.Max(worstDbDelay, arrivalDelay);
            }
            deutscheBahned.Add(new ProgressPoint(trip.Date, Math.Round(worstDbDelay, 0)));

            if (trip.Distance > 0 && trip.Distance < 2)
            {
                shortHopCount++;
            }
            shortHops.Add(new ProgressPoint(trip.Date, shortHopCount));

            seenDays.Add(trip.Date.Date);
            travelDays.Add(new ProgressPoint(trip.Date, seenDays.Count));
        }

        var (marathonDay, operatorBingo, streak) = BuildPerDayProgressions(trips
            .Select(t => (t.Date, t.Distance, Operators: SplitOperators(t.Operator).ToList()))
            .ToList());

        result.Families =
        [
            BuildFamily("DISTANCE", "straighten", "km", [1000, 5000, 10000, 25000, 50000, 100000], distance),
            BuildFamily("TRIPS", "confirmation_number", "count", [10, 50, 100, 500, 1000], tripCount),
            BuildFamily("TRAVEL_DAYS", "event_available", "count", [10, 50, 100, 365], travelDays),
            BuildFamily("STREAK", "local_fire_department", "count", [3, 7, 14, 30], streak),
            BuildFamily("COUNTRIES", "public", "count", [1, 3, 5, 10, 25], countries),
            BuildFamily("BORDER_HOPPER", "swap_horiz", "count", [1, 10, 50], borderHops),
            BuildFamily("OPERATORS", "badge", "count", [5, 10, 25, 50], operatorsSeen),
            BuildFamily("OPERATOR_BINGO", "casino", "count", [3, 5, 8], operatorBingo),
            BuildFamily("LONGEST_TRIP", "trending_flat", "km", [100, 250, 500, 1000], longestTrip),
            BuildFamily("MARATHON_DAY", "directions_run", "km", [250, 500, 1000], marathonDay),
            BuildFamily("TOP_SPEED", "speed", "kmh", [80, 100, 160, 200], topSpeed),
            BuildFamily("NIGHT_TRAINS", "bedtime", "count", [1, 5, 10, 25], nightTrains),
            BuildFamily("DELAY_TIME", "hourglass_bottom", "hours", [1, 5, 24, 72], delayHours),
            BuildFamily("DEUTSCHEBAHNED", "sentiment_dissatisfied", "minutes", [30, 60, 120, 180, 300], deutscheBahned),
            BuildFamily("SHORT_HOP", "directions_walk", "count", [1, 10, 50], shortHops),
        ];

        foreach (var group in CountryGroups)
        {
            result.Families.Add(BuildFamily(group.Key, group.Icon, "count", [group.Iso.Length], countryGroupProgress[group.Key]));
        }

        result.Families.AddRange(BuildSubdivisionFamilies(trips.Select(t => (t.Date, t.RouteId)).ToList(),
            routeId => routeRegions.TryGetValue(routeId, out var route) ? route.RegionIds : [],
            subdivisionParent, countryInfo));

        return result;
    }

    /// <summary>
    /// One "collect the regions" family per country the user has actually been to a subdivision
    /// of - provinces, Bundesländer, cantons, régions. Thresholds are a proportion of that
    /// country's own subdivision count, so a 3-region country and a 26-canton one both work.
    /// </summary>
    internal static List<AchievementFamilyDTO> BuildSubdivisionFamilies(
        IReadOnlyList<(DateTime Date, int RouteId)> trips,
        Func<int, IReadOnlyList<int>> regionsForRoute,
        IReadOnlyDictionary<int, int> subdivisionParent,
        IReadOnlyDictionary<int, (string Name, string NameNL, int Total)> countryInfo)
    {
        // First pass: which countries are worth showing at all.
        var touched = new Dictionary<int, HashSet<int>>();
        foreach (var trip in trips)
        {
            foreach (var regionId in regionsForRoute(trip.RouteId))
            {
                if (subdivisionParent.TryGetValue(regionId, out var countryId))
                {
                    if (!touched.TryGetValue(countryId, out var set))
                    {
                        set = [];
                        touched[countryId] = set;
                    }
                    set.Add(regionId);
                }
            }
        }

        var families = new List<AchievementFamilyDTO>();
        foreach (var (countryId, _) in touched.OrderByDescending(t => t.Value.Count))
        {
            if (!countryInfo.TryGetValue(countryId, out var country) || country.Total == 0)
            {
                continue;
            }

            // Second pass, for this country only, so each threshold can still be dated.
            var progression = new List<ProgressPoint>(trips.Count);
            var seen = new HashSet<int>();
            foreach (var trip in trips)
            {
                foreach (var regionId in regionsForRoute(trip.RouteId))
                {
                    if (subdivisionParent.TryGetValue(regionId, out var parent) && parent == countryId)
                    {
                        seen.Add(regionId);
                    }
                }
                progression.Add(new ProgressPoint(trip.Date, seen.Count));
            }

            var family = BuildFamily($"SUBDIVISIONS_{countryId}", "flag", "count",
                SubdivisionThresholds(country.Total), progression);
            family.Name = country.Name;
            family.NameNL = country.NameNL;
            family.DescriptionKey = "SUBDIVISIONS_DESC";
            families.Add(family);
        }

        return families;
    }

    internal readonly record struct RegionNode(int Id, int? ParentRegionId, string Name, string NameNL, string IsoCode);

    /// <summary>A collection should be granular enough to be interesting, small enough to finish.</summary>
    internal const int MinCollectible = 5;
    internal const int MaxCollectible = 60;

    /// <summary>
    /// Picks, per country, which level of the region tree to collect, and returns a lookup from
    /// region id to country id plus the display data for each country.
    ///
    /// Countries do not agree on what their "first level" means: the Netherlands has twelve
    /// provinces directly under it, while Belgium has three regions whose children are the
    /// provinces people actually collect. Rather than hard-code that, take the deepest level that
    /// still forms a sensible collection.
    /// </summary>
    internal static (Dictionary<int, int> RegionToCountry, Dictionary<int, (string Name, string NameNL, int Total)> Countries)
        ResolveCollectibleRegions(IReadOnlyList<RegionNode> regions)
    {
        var childrenByParent = regions
            .Where(r => r.ParentRegionId.HasValue)
            .GroupBy(r => r.ParentRegionId.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var regionToCountry = new Dictionary<int, int>();
        var countries = new Dictionary<int, (string, string, int)>();

        // A country is a top-level region carrying an ISO code.
        foreach (var country in regions.Where(r => !r.ParentRegionId.HasValue && !string.IsNullOrWhiteSpace(r.IsoCode)))
        {
            var levels = new List<List<RegionNode>>();
            var current = childrenByParent.TryGetValue(country.Id, out var firstLevel) ? firstLevel : [];
            while (current.Count > 0 && levels.Count < 3)
            {
                levels.Add(current);
                current = current
                    .SelectMany(node => childrenByParent.TryGetValue(node.Id, out var kids) ? kids : [])
                    .ToList();
            }

            if (levels.Count == 0)
            {
                continue;
            }

            var chosen = levels[ChooseCollectLevel([.. levels.Select(l => l.Count)])];
            foreach (var region in chosen)
            {
                regionToCountry[region.Id] = country.Id;
            }
            countries[country.Id] = (country.Name, country.NameNL, chosen.Count);
        }

        return (regionToCountry, countries);
    }

    /// <summary>
    /// Normally the level directly under the country: in this database that is already the level
    /// people collect (Dutch and Belgian provinces, German Bundesländer, Swiss cantons).
    ///
    /// Only when that level is uselessly small - the United Kingdom's four nations - is a deeper
    /// level considered, and then only the shallowest one that forms a sensible collection.
    /// Deliberately conservative: a country whose top level is already fine never descends, so
    /// importing a finer level later cannot silently change what an achievement means.
    /// </summary>
    internal static int ChooseCollectLevel(IReadOnlyList<int> countsPerLevel)
    {
        if (countsPerLevel.Count == 0 || countsPerLevel[0] >= MinCollectible)
        {
            return 0;
        }
        for (var index = 1; index < countsPerLevel.Count; index++)
        {
            if (countsPerLevel[index] >= MinCollectible && countsPerLevel[index] <= MaxCollectible)
            {
                return index;
            }
        }
        return 0;
    }

    /// <summary>Quarter, half, three quarters and all of a country's subdivisions.</summary>
    internal static List<double> SubdivisionThresholds(int total)
    {
        return new[] { 0.25, 0.5, 0.75, 1.0 }
            .Select(fraction => (double)Math.Max(1, (int)Math.Ceiling(total * fraction)))
            .Distinct()
            .OrderBy(value => value)
            .ToList();
    }

    /// <summary>
    /// Families measured per calendar day: the biggest day so far, the most operators used in a
    /// single day, and the longest run of consecutive travel days.
    /// </summary>
    internal static (List<ProgressPoint> MarathonDay, List<ProgressPoint> OperatorBingo, List<ProgressPoint> Streak)
        BuildPerDayProgressions(IReadOnlyList<(DateTime Date, double Distance, List<string> Operators)> trips)
    {
        var byDay = trips
            .GroupBy(t => t.Date.Date)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Day = g.Key,
                Distance = g.Sum(t => t.Distance),
                Operators = g.SelectMany(t => t.Operators).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            })
            .ToList();

        var marathon = new List<ProgressPoint>(byDay.Count);
        var bingo = new List<ProgressPoint>(byDay.Count);
        var streak = new List<ProgressPoint>(byDay.Count);

        double bestDistance = 0;
        var bestOperators = 0;
        var bestStreak = 0;
        var currentStreak = 0;
        DateTime? previousDay = null;

        foreach (var day in byDay)
        {
            bestDistance = Math.Max(bestDistance, day.Distance);
            marathon.Add(new ProgressPoint(day.Day, Math.Round(bestDistance, 1)));

            bestOperators = Math.Max(bestOperators, day.Operators);
            bingo.Add(new ProgressPoint(day.Day, bestOperators));

            currentStreak = previousDay.HasValue && day.Day == previousDay.Value.AddDays(1)
                ? currentStreak + 1
                : 1;
            previousDay = day.Day;
            bestStreak = Math.Max(bestStreak, currentStreak);
            streak.Add(new ProgressPoint(day.Day, bestStreak));
        }

        return (marathon, bingo, streak);
    }

    private async Task<HashSet<int>> FindDeutscheBahnOperatorIdsAsync(CancellationToken cancellationToken)
    {
        // Operator.Names is stored through a value conversion, so it cannot be filtered in SQL.
        // The table is small, so match in memory against the curated names instead of the free
        // text on the route.
        var operators = await dbContext.Operators
            .AsNoTracking()
            .Select(o => new { o.Id, o.Names })
            .ToListAsync(cancellationToken);

        return operators
            .Where(o => o.Names != null && o.Names.Any(IsDeutscheBahnName))
            .Select(o => o.Id)
            .ToHashSet();
    }

    internal static bool IsDeutscheBahnName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }
        var trimmed = name.Trim();
        return string.Equals(trimmed, "DB", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("Deutsche Bahn", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("DB ", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Prefers the mapped Operators relation; falls back to the free-text operating company only
    /// for routes that have not been mapped yet.
    /// </summary>
    internal static bool IsDeutscheBahn(IEnumerable<int> routeOperatorIds, string operatingCompany, IReadOnlySet<int> deutscheBahnOperatorIds)
    {
        var ids = routeOperatorIds?.ToList() ?? [];
        if (ids.Count > 0)
        {
            return ids.Any(deutscheBahnOperatorIds.Contains);
        }
        return SplitOperators(operatingCompany).Any(IsDeutscheBahnName);
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
