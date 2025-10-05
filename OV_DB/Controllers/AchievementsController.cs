using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OV_DB.Models;
using OV_DB.Services;
using OVDB_database.Database;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AchievementsController : ControllerBase
    {
        private readonly OVDBDatabaseContext _context;
        private readonly IAchievementService _achievementService;
        private readonly IAchievementIconService _iconService;

        public AchievementsController(
            OVDBDatabaseContext context, 
            IAchievementService achievementService,
            IAchievementIconService iconService)
        {
            _context = context;
            _achievementService = achievementService;
            _iconService = iconService;
        }

        [HttpGet]
        public async Task<ActionResult<List<AchievementProgressDTO>>> GetAchievements()
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }

            // Get all achievements
            var allAchievements = await _achievementService.GetAllAchievementsAsync();
            
            // Get user's unlocked achievements
            var userAchievements = await _achievementService.GetUserAchievementsAsync(userIdClaim);
            var unlockedAchievementIds = userAchievements.Select(ua => ua.AchievementId).ToHashSet();

            // Calculate current progress for each category
            var totalDistance = await CalculateTotalDistanceAsync(userIdClaim);
            var stationCount = await CalculateUniqueStationsAsync(userIdClaim);
            var countryCount = await CalculateUniqueCountriesAsync(userIdClaim);
            var transportTypeCount = await CalculateUniqueTransportTypesAsync(userIdClaim);

            var categoryProgress = new Dictionary<string, int>
            {
                { "distance_overall", totalDistance },
                { "stations", stationCount },
                { "countries", countryCount },
                { "transport_types", transportTypeCount }
            };

            // Group achievements by category
            var achievementsByCategory = allAchievements
                .GroupBy(a => a.Category)
                .Select(g => new AchievementProgressDTO
                {
                    Category = g.Key,
                    CurrentValue = categoryProgress.ContainsKey(g.Key) ? categoryProgress[g.Key] : 0,
                    Achievements = g.Select(a =>
                    {
                        var userAchievement = userAchievements.FirstOrDefault(ua => ua.AchievementId == a.Id);
                        return new AchievementDTO
                        {
                            Id = a.Id,
                            Key = a.Key,
                            Name = a.Name,
                            NameNL = a.NameNL,
                            Description = a.Description,
                            DescriptionNL = a.DescriptionNL,
                            Category = a.Category,
                            Level = a.Level,
                            IconName = a.IconName,
                            IconUrl = a.IconUrl,
                            ThresholdValue = a.ThresholdValue,
                            CurrentProgress = categoryProgress.ContainsKey(a.Category) ? categoryProgress[a.Category] : 0,
                            IsUnlocked = unlockedAchievementIds.Contains(a.Id),
                            UnlockedAt = userAchievement?.UnlockedAt,
                            Year = userAchievement?.Year
                        };
                    }).ToList()
                })
                .ToList();

            return Ok(achievementsByCategory);
        }

        [HttpGet("unlocked")]
        public async Task<ActionResult<List<AchievementDTO>>> GetUnlockedAchievements()
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }

            var userAchievements = await _context.UserAchievements
                .Include(ua => ua.Achievement)
                .Where(ua => ua.UserId == userIdClaim)
                .OrderByDescending(ua => ua.UnlockedAt)
                .ToListAsync();

            var result = userAchievements.Select(ua => new AchievementDTO
            {
                Id = ua.Achievement.Id,
                Key = ua.Achievement.Key,
                Name = ua.Achievement.Name,
                NameNL = ua.Achievement.NameNL,
                Description = ua.Achievement.Description,
                DescriptionNL = ua.Achievement.DescriptionNL,
                Category = ua.Achievement.Category,
                Level = ua.Achievement.Level,
                IconName = ua.Achievement.IconName,
                IconUrl = ua.Achievement.IconUrl,
                ThresholdValue = ua.Achievement.ThresholdValue,
                CurrentProgress = ua.CurrentProgress,
                IsUnlocked = true,
                UnlockedAt = ua.UnlockedAt,
                Year = ua.Year
            }).ToList();

            return Ok(result);
        }

        [HttpPost("check")]
        public async Task<ActionResult> CheckAchievements()
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }

            await _achievementService.CheckAndAwardAchievementsAsync(userIdClaim);
            return Ok();
        }

        [HttpPost("generate-icons")]
        public async Task<ActionResult> GenerateAchievementIcons()
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }

            var achievements = await _context.Achievements.ToListAsync();
            
            foreach (var achievement in achievements)
            {
                var iconUrl = await _iconService.GenerateAchievementIconAsync(achievement.Key, achievement.Level);
                if (!string.IsNullOrEmpty(iconUrl))
                {
                    achievement.IconUrl = iconUrl;
                }
            }

            await _context.SaveChangesAsync();
            
            return Ok(new { Message = "Achievement icons generated successfully", Count = achievements.Count });
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
    }
}
