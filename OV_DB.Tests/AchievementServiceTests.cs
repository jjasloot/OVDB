using OV_DB.Services;
using OVDB_database.Models;
using OVDB_database.Database;
using Microsoft.EntityFrameworkCore;

namespace OV_DB.Tests
{
    public class AchievementServiceTests
    {
        private OVDBDatabaseContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<OVDBDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new OVDBDatabaseContext(options);
        }

        [Fact]
        public async Task InitializeAchievementsAsync_CreatesAchievements()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var service = new AchievementService(context);

            // Act
            await service.InitializeAchievementsAsync();

            // Assert
            var achievements = await context.Achievements.ToListAsync();
            Assert.NotEmpty(achievements);
            Assert.Contains(achievements, a => a.Category == "distance_overall");
            Assert.Contains(achievements, a => a.Category == "stations");
            Assert.Contains(achievements, a => a.Category == "countries");
            Assert.Contains(achievements, a => a.Category == "transport_types");
        }

        [Fact]
        public async Task InitializeAchievementsAsync_DoesNotDuplicateAchievements()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var service = new AchievementService(context);

            // Act - Initialize twice
            await service.InitializeAchievementsAsync();
            var countAfterFirst = await context.Achievements.CountAsync();
            
            await service.InitializeAchievementsAsync();
            var countAfterSecond = await context.Achievements.CountAsync();

            // Assert - Should be the same count
            Assert.Equal(countAfterFirst, countAfterSecond);
        }

        [Fact]
        public async Task GetAllAchievementsAsync_ReturnsOrderedList()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var service = new AchievementService(context);
            await service.InitializeAchievementsAsync();

            // Act
            var achievements = await service.GetAllAchievementsAsync();

            // Assert
            Assert.NotEmpty(achievements);
            // Verify they are ordered by DisplayOrder
            var previousDisplayOrder = -1;
            foreach (var achievement in achievements)
            {
                Assert.True(achievement.DisplayOrder >= previousDisplayOrder);
                previousDisplayOrder = achievement.DisplayOrder;
            }
        }

        [Fact]
        public async Task CheckAndAwardAchievementsAsync_WithNoProgress_AwardsNoAchievements()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var service = new AchievementService(context);
            await service.InitializeAchievementsAsync();

            // Create a user with no activity
            var user = new User { Id = 1, Email = "test@example.com" };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Act
            await service.CheckAndAwardAchievementsAsync(user.Id);

            // Assert
            var userAchievements = await service.GetUserAchievementsAsync(user.Id);
            Assert.Empty(userAchievements);
        }

        [Fact]
        public void Achievement_HasRequiredProperties()
        {
            // Arrange & Act
            var achievement = new Achievement
            {
                Key = "test_achievement",
                Name = "Test Achievement",
                NameNL = "Test Prestatie",
                Description = "Test description",
                DescriptionNL = "Test beschrijving",
                Category = "test",
                Level = "bronze",
                IconName = "test_icon",
                ThresholdValue = 100,
                DisplayOrder = 1
            };

            // Assert
            Assert.Equal("test_achievement", achievement.Key);
            Assert.Equal("Test Achievement", achievement.Name);
            Assert.Equal("Test Prestatie", achievement.NameNL);
            Assert.Equal("test", achievement.Category);
            Assert.Equal("bronze", achievement.Level);
            Assert.Equal(100, achievement.ThresholdValue);
        }

        [Fact]
        public void UserAchievement_HasRequiredProperties()
        {
            // Arrange & Act
            var userAchievement = new UserAchievement
            {
                UserId = 1,
                AchievementId = 1,
                UnlockedAt = DateTime.UtcNow,
                CurrentProgress = 150
            };

            // Assert
            Assert.Equal(1, userAchievement.UserId);
            Assert.Equal(1, userAchievement.AchievementId);
            Assert.Equal(150, userAchievement.CurrentProgress);
            Assert.True(userAchievement.UnlockedAt <= DateTime.UtcNow);
        }
    }
}
