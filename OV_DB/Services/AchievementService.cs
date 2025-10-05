using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Services
{
    public interface IAchievementService
    {
        Task InitializeAchievementsAsync();
        Task CheckAndAwardAchievementsAsync(int userId);
        Task<List<Achievement>> GetAllAchievementsAsync();
        Task<List<UserAchievement>> GetUserAchievementsAsync(int userId);
    }

    public class AchievementService : IAchievementService
    {
        private readonly OVDBDatabaseContext _context;

        public AchievementService(OVDBDatabaseContext context)
        {
            _context = context;
        }

        public async Task InitializeAchievementsAsync()
        {
            // Check if achievements already exist
            if (await _context.Achievements.AnyAsync())
            {
                return;
            }

            var achievements = GetPredefinedAchievements();
            await _context.Achievements.AddRangeAsync(achievements);
            await _context.SaveChangesAsync();
        }

        private List<Achievement> GetPredefinedAchievements()
        {
            var achievements = new List<Achievement>();
            var displayOrder = 1;

            // Distance Achievements - Overall
            achievements.AddRange(new[]
            {
                new Achievement
                {
                    Key = "distance_bronze",
                    Name = "Commuter",
                    NameNL = "Forenzen",
                    Description = "Travel 1,000 km in total",
                    DescriptionNL = "Reis 1.000 km in totaal",
                    Category = "distance_overall",
                    Level = "bronze",
                    IconName = "commute",
                    ThresholdValue = 1000,
                    DisplayOrder = displayOrder++
                },
                new Achievement
                {
                    Key = "distance_silver",
                    Name = "Voyager",
                    NameNL = "Reiziger",
                    Description = "Travel 10,000 km in total",
                    DescriptionNL = "Reis 10.000 km in totaal",
                    Category = "distance_overall",
                    Level = "silver",
                    IconName = "flight",
                    ThresholdValue = 10000,
                    DisplayOrder = displayOrder++
                },
                new Achievement
                {
                    Key = "distance_gold",
                    Name = "Globetrotter",
                    NameNL = "Globetrotter",
                    Description = "Travel 50,000 km in total",
                    DescriptionNL = "Reis 50.000 km in totaal",
                    Category = "distance_overall",
                    Level = "gold",
                    IconName = "public",
                    ThresholdValue = 50000,
                    DisplayOrder = displayOrder++
                },
                new Achievement
                {
                    Key = "distance_platinum",
                    Name = "Cosmic Explorer",
                    NameNL = "Kosmische Ontdekkingsreiziger",
                    Description = "Travel 100,000 km in total",
                    DescriptionNL = "Reis 100.000 km in totaal",
                    Category = "distance_overall",
                    Level = "platinum",
                    IconName = "rocket_launch",
                    ThresholdValue = 100000,
                    DisplayOrder = displayOrder++
                },
                new Achievement
                {
                    Key = "distance_diamond",
                    Name = "Interstellar Traveler",
                    NameNL = "Interstellaire Reiziger",
                    Description = "Travel 500,000 km in total",
                    DescriptionNL = "Reis 500.000 km in totaal",
                    Category = "distance_overall",
                    Level = "diamond",
                    IconName = "stars",
                    ThresholdValue = 500000,
                    DisplayOrder = displayOrder++
                }
            });

            // Distance Achievements - Yearly
            achievements.AddRange(new[]
            {
                new Achievement
                {
                    Key = "distance_yearly_bronze",
                    Name = "Yearly Commuter",
                    NameNL = "Jaarlijkse Forenzen",
                    Description = "Travel 1,000 km in a single year",
                    DescriptionNL = "Reis 1.000 km in een jaar",
                    Category = "distance_yearly",
                    Level = "bronze",
                    IconName = "calendar_month",
                    ThresholdValue = 1000,
                    DisplayOrder = displayOrder++
                },
                new Achievement
                {
                    Key = "distance_yearly_silver",
                    Name = "Yearly Voyager",
                    NameNL = "Jaarlijkse Reiziger",
                    Description = "Travel 5,000 km in a single year",
                    DescriptionNL = "Reis 5.000 km in een jaar",
                    Category = "distance_yearly",
                    Level = "silver",
                    IconName = "event",
                    ThresholdValue = 5000,
                    DisplayOrder = displayOrder++
                },
                new Achievement
                {
                    Key = "distance_yearly_gold",
                    Name = "Yearly Explorer",
                    NameNL = "Jaarlijkse Ontdekker",
                    Description = "Travel 10,000 km in a single year",
                    DescriptionNL = "Reis 10.000 km in een jaar",
                    Category = "distance_yearly",
                    Level = "gold",
                    IconName = "today",
                    ThresholdValue = 10000,
                    DisplayOrder = displayOrder++
                },
                new Achievement
                {
                    Key = "distance_yearly_platinum",
                    Name = "Yearly Globetrotter",
                    NameNL = "Jaarlijkse Globetrotter",
                    Description = "Travel 25,000 km in a single year",
                    DescriptionNL = "Reis 25.000 km in een jaar",
                    Category = "distance_yearly",
                    Level = "platinum",
                    IconName = "date_range",
                    ThresholdValue = 25000,
                    DisplayOrder = displayOrder++
                },
                new Achievement
                {
                    Key = "distance_yearly_diamond",
                    Name = "Yearly Champion",
                    NameNL = "Jaarlijkse Kampioen",
                    Description = "Travel 50,000 km in a single year",
                    DescriptionNL = "Reis 50.000 km in een jaar",
                    Category = "distance_yearly",
                    Level = "diamond",
                    IconName = "celebration",
                    ThresholdValue = 50000,
                    DisplayOrder = displayOrder++
                }
            });

            // Station Collector Achievements
            achievements.AddRange(new[]
            {
                new Achievement
                {
                    Key = "stations_10",
                    Name = "Station Starter",
                    NameNL = "Station Starter",
                    Description = "Visit 10 unique stations",
                    DescriptionNL = "Bezoek 10 unieke stations",
                    Category = "stations",
                    Level = "bronze",
                    IconName = "location_on",
                    ThresholdValue = 10,
                    DisplayOrder = displayOrder++
                },
                new Achievement
                {
                    Key = "stations_50",
                    Name = "Station Explorer",
                    NameNL = "Station Ontdekker",
                    Description = "Visit 50 unique stations",
                    DescriptionNL = "Bezoek 50 unieke stations",
                    Category = "stations",
                    Level = "silver",
                    IconName = "map",
                    ThresholdValue = 50,
                    DisplayOrder = displayOrder++
                },
                new Achievement
                {
                    Key = "stations_100",
                    Name = "Station Collector",
                    NameNL = "Station Verzamelaar",
                    Description = "Visit 100 unique stations",
                    DescriptionNL = "Bezoek 100 unieke stations",
                    Category = "stations",
                    Level = "gold",
                    IconName = "train",
                    ThresholdValue = 100,
                    DisplayOrder = displayOrder++
                },
                new Achievement
                {
                    Key = "stations_500",
                    Name = "Station Master",
                    NameNL = "Station Meester",
                    Description = "Visit 500 unique stations",
                    DescriptionNL = "Bezoek 500 unieke stations",
                    Category = "stations",
                    Level = "platinum",
                    IconName = "transfer_within_a_station",
                    ThresholdValue = 500,
                    DisplayOrder = displayOrder++
                }
            });

            // Country Achievements
            achievements.AddRange(new[]
            {
                new Achievement
                {
                    Key = "countries_3",
                    Name = "Neighbor Visitor",
                    NameNL = "Buurland Bezoeker",
                    Description = "Visit 3 different countries",
                    DescriptionNL = "Bezoek 3 verschillende landen",
                    Category = "countries",
                    Level = "bronze",
                    IconName = "flag",
                    ThresholdValue = 3,
                    DisplayOrder = displayOrder++
                },
                new Achievement
                {
                    Key = "countries_5",
                    Name = "Country Hopper",
                    NameNL = "Land Springer",
                    Description = "Visit 5 different countries",
                    DescriptionNL = "Bezoek 5 verschillende landen",
                    Category = "countries",
                    Level = "silver",
                    IconName = "language",
                    ThresholdValue = 5,
                    DisplayOrder = displayOrder++
                },
                new Achievement
                {
                    Key = "countries_10",
                    Name = "International Traveler",
                    NameNL = "Internationale Reiziger",
                    Description = "Visit 10 different countries",
                    DescriptionNL = "Bezoek 10 verschillende landen",
                    Category = "countries",
                    Level = "gold",
                    IconName = "travel_explore",
                    ThresholdValue = 10,
                    DisplayOrder = displayOrder++
                },
                new Achievement
                {
                    Key = "countries_25",
                    Name = "World Explorer",
                    NameNL = "Wereld Ontdekker",
                    Description = "Visit 25 different countries",
                    DescriptionNL = "Bezoek 25 verschillende landen",
                    Category = "countries",
                    Level = "platinum",
                    IconName = "public",
                    ThresholdValue = 25,
                    DisplayOrder = displayOrder++
                }
            });

            // Transport Type Achievements
            achievements.AddRange(new[]
            {
                new Achievement
                {
                    Key = "transport_types_3",
                    Name = "Multimodal Beginner",
                    NameNL = "Multimodaal Beginner",
                    Description = "Use 3 different transport types",
                    DescriptionNL = "Gebruik 3 verschillende vervoerstypes",
                    Category = "transport_types",
                    Level = "bronze",
                    IconName = "directions_transit",
                    ThresholdValue = 3,
                    DisplayOrder = displayOrder++
                },
                new Achievement
                {
                    Key = "transport_types_5",
                    Name = "Transport Enthusiast",
                    NameNL = "Vervoer Liefhebber",
                    Description = "Use 5 different transport types",
                    DescriptionNL = "Gebruik 5 verschillende vervoerstypes",
                    Category = "transport_types",
                    Level = "silver",
                    IconName = "directions",
                    ThresholdValue = 5,
                    DisplayOrder = displayOrder++
                },
                new Achievement
                {
                    Key = "transport_types_7",
                    Name = "Multimodal Master",
                    NameNL = "Multimodaal Meester",
                    Description = "Use 7 different transport types",
                    DescriptionNL = "Gebruik 7 verschillende vervoerstypes",
                    Category = "transport_types",
                    Level = "gold",
                    IconName = "alt_route",
                    ThresholdValue = 7,
                    DisplayOrder = displayOrder++
                },
                new Achievement
                {
                    Key = "transport_types_10",
                    Name = "Transport Completionist",
                    NameNL = "Vervoer Verzamelaar",
                    Description = "Use 10 different transport types",
                    DescriptionNL = "Gebruik 10 verschillende vervoerstypes",
                    Category = "transport_types",
                    Level = "platinum",
                    IconName = "emoji_transportation",
                    ThresholdValue = 10,
                    DisplayOrder = displayOrder++
                }
            });

            return achievements;
        }

        public async Task CheckAndAwardAchievementsAsync(int userId)
        {
            var allAchievements = await _context.Achievements.ToListAsync();
            var userAchievements = await _context.UserAchievements
                .Where(ua => ua.UserId == userId)
                .ToListAsync();

            var newAchievements = new List<UserAchievement>();

            // Calculate user stats
            var totalDistance = await CalculateTotalDistanceAsync(userId);
            var stationCount = await CalculateUniqueStationsAsync(userId);
            var countryCount = await CalculateUniqueCountriesAsync(userId);
            var transportTypeCount = await CalculateUniqueTransportTypesAsync(userId);

            // Check distance achievements
            await CheckCategoryAchievements(userId, "distance_overall", totalDistance, 
                allAchievements, userAchievements, newAchievements);

            // Check yearly distance achievements
            await CheckYearlyDistanceAchievements(userId, allAchievements, userAchievements, newAchievements);

            // Check station achievements
            await CheckCategoryAchievements(userId, "stations", stationCount, 
                allAchievements, userAchievements, newAchievements);

            // Check country achievements
            await CheckCategoryAchievements(userId, "countries", countryCount, 
                allAchievements, userAchievements, newAchievements);

            // Check transport type achievements
            await CheckCategoryAchievements(userId, "transport_types", transportTypeCount, 
                allAchievements, userAchievements, newAchievements);

            if (newAchievements.Any())
            {
                await _context.UserAchievements.AddRangeAsync(newAchievements);
                await _context.SaveChangesAsync();
            }
        }

        private async Task CheckCategoryAchievements(
            int userId, 
            string category, 
            int currentValue,
            List<Achievement> allAchievements,
            List<UserAchievement> userAchievements,
            List<UserAchievement> newAchievements)
        {
            var categoryAchievements = allAchievements
                .Where(a => a.Category == category)
                .OrderBy(a => a.ThresholdValue)
                .ToList();

            foreach (var achievement in categoryAchievements)
            {
                var alreadyUnlocked = userAchievements.Any(ua => ua.AchievementId == achievement.Id);
                
                if (!alreadyUnlocked && currentValue >= achievement.ThresholdValue)
                {
                    newAchievements.Add(new UserAchievement
                    {
                        UserId = userId,
                        AchievementId = achievement.Id,
                        UnlockedAt = DateTime.UtcNow,
                        CurrentProgress = currentValue
                    });
                }
            }
        }

        private async Task CheckYearlyDistanceAchievements(
            int userId,
            List<Achievement> allAchievements,
            List<UserAchievement> userAchievements,
            List<UserAchievement> newAchievements)
        {
            var yearlyAchievements = allAchievements
                .Where(a => a.Category == "distance_yearly")
                .OrderBy(a => a.ThresholdValue)
                .ToList();

            if (!yearlyAchievements.Any())
                return;

            // Get distance per year for the user
            var yearlyDistances = await _context.RouteInstances
                .Where(ri => ri.Route.RouteMaps.Any(rm => rm.Map.UserId == userId))
                .GroupBy(ri => ri.Date.Year)
                .Select(g => new
                {
                    Year = g.Key,
                    Distance = g.Sum(ri => (ri.Route.OverrideDistance.HasValue && ri.Route.OverrideDistance > 0)
                        ? ri.Route.OverrideDistance.Value
                        : ri.Route.CalculatedDistance)
                })
                .ToListAsync();

            foreach (var yearData in yearlyDistances)
            {
                var yearDistance = (int)yearData.Distance;
                
                foreach (var achievement in yearlyAchievements)
                {
                    // Check if user already has this achievement for this year
                    var alreadyUnlocked = userAchievements.Any(ua => 
                        ua.AchievementId == achievement.Id && 
                        ua.Year == yearData.Year);
                    
                    if (!alreadyUnlocked && yearDistance >= achievement.ThresholdValue)
                    {
                        newAchievements.Add(new UserAchievement
                        {
                            UserId = userId,
                            AchievementId = achievement.Id,
                            UnlockedAt = DateTime.UtcNow,
                            CurrentProgress = yearDistance,
                            Year = yearData.Year
                        });
                    }
                }
            }
        }

        private async Task<int> CalculateTotalDistanceAsync(int userId)
        {
            var distance = await _context.RouteInstances
                .Where(ri => ri.Route.RouteMaps.Any(rm => rm.Map.UserId == userId))
                .Select(ri => (ri.Route.OverrideDistance.HasValue && ri.Route.OverrideDistance > 0) 
                    ? ri.Route.OverrideDistance.Value 
                    : ri.Route.CalculatedDistance)
                .SumAsync();

            return (int)distance;
        }

        private async Task<int> CalculateUniqueStationsAsync(int userId)
        {
            return await _context.StationVisits
                .Where(sv => sv.UserId == userId)
                .Select(sv => sv.StationId)
                .Distinct()
                .CountAsync();
        }

        private async Task<int> CalculateUniqueCountriesAsync(int userId)
        {
            return await _context.StationVisits
                .Where(sv => sv.UserId == userId)
                .Select(sv => sv.Station.StationCountry.Name)
                .Distinct()
                .CountAsync();
        }

        private async Task<int> CalculateUniqueTransportTypesAsync(int userId)
        {
            return await _context.RouteInstances
                .Where(ri => ri.Route.RouteMaps.Any(rm => rm.Map.UserId == userId))
                .Select(ri => ri.Route.RouteTypeId)
                .Distinct()
                .CountAsync();
        }

        public async Task<List<Achievement>> GetAllAchievementsAsync()
        {
            return await _context.Achievements
                .OrderBy(a => a.DisplayOrder)
                .ToListAsync();
        }

        public async Task<List<UserAchievement>> GetUserAchievementsAsync(int userId)
        {
            return await _context.UserAchievements
                .Include(ua => ua.Achievement)
                .Where(ua => ua.UserId == userId)
                .ToListAsync();
        }
    }
}
