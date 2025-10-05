-- OVDB Achievement System - Utility Queries
-- This file contains useful SQL queries for managing and querying achievements

-- =============================================================================
-- 1. Check all available achievements
-- =============================================================================
SELECT 
    Id,
    `Key`,
    Name,
    Category,
    Level,
    ThresholdValue,
    DisplayOrder
FROM Achievements
ORDER BY Category, DisplayOrder;

-- =============================================================================
-- 2. Get user statistics for achievement calculation
-- =============================================================================
-- Total distance for a user (replace USER_ID with actual user ID)
SELECT 
    SUM(COALESCE(r.OverrideDistance, r.CalculatedDistance)) AS TotalDistance
FROM RouteInstances ri
JOIN Routes r ON ri.RouteId = r.Id
JOIN RoutesMaps rm ON r.Id = rm.RouteId
WHERE rm.MapUserId = USER_ID;

-- Unique stations visited by a user
SELECT COUNT(DISTINCT sv.StationId) AS UniqueStations
FROM StationVisits sv
WHERE sv.UserId = USER_ID;

-- Unique countries visited by a user
SELECT COUNT(DISTINCT s.StationCountryId) AS UniqueCountries
FROM StationVisits sv
JOIN Stations s ON sv.StationId = s.Id
WHERE sv.UserId = USER_ID;

-- Unique transport types used by a user
SELECT COUNT(DISTINCT r.RouteTypeId) AS UniqueTransportTypes
FROM RouteInstances ri
JOIN Routes r ON ri.RouteId = r.Id
JOIN RoutesMaps rm ON r.Id = rm.RouteId
WHERE rm.MapUserId = USER_ID;

-- =============================================================================
-- 3. Get achievements for a specific user
-- =============================================================================
SELECT 
    a.Name,
    a.NameNL,
    a.Category,
    a.Level,
    a.ThresholdValue,
    ua.UnlockedAt,
    ua.CurrentProgress
FROM UserAchievements ua
JOIN Achievements a ON ua.AchievementId = a.Id
WHERE ua.UserId = USER_ID
ORDER BY ua.UnlockedAt DESC;

-- =============================================================================
-- 4. Get achievement progress for all users
-- =============================================================================
SELECT 
    u.Email,
    COUNT(ua.Id) AS UnlockedAchievements,
    (SELECT COUNT(*) FROM Achievements) AS TotalAchievements,
    CONCAT(ROUND(COUNT(ua.Id) * 100.0 / (SELECT COUNT(*) FROM Achievements), 2), '%') AS CompletionPercentage
FROM Users u
LEFT JOIN UserAchievements ua ON u.Id = ua.UserId
GROUP BY u.Id, u.Email
ORDER BY UnlockedAchievements DESC;

-- =============================================================================
-- 5. Find users eligible for specific achievements (but not yet awarded)
-- =============================================================================
-- Example: Users eligible for "Bronze Distance" achievement (1000 km) but haven't received it

WITH UserStats AS (
    SELECT 
        rm.MapUserId AS UserId,
        SUM(COALESCE(r.OverrideDistance, r.CalculatedDistance)) AS TotalDistance
    FROM RouteInstances ri
    JOIN Routes r ON ri.RouteId = r.Id
    JOIN RoutesMaps rm ON r.Id = rm.RouteId
    GROUP BY rm.MapUserId
)
SELECT 
    u.Email,
    us.TotalDistance
FROM UserStats us
JOIN Users u ON us.UserId = u.Id
WHERE us.TotalDistance >= 1000
AND NOT EXISTS (
    SELECT 1 
    FROM UserAchievements ua
    JOIN Achievements a ON ua.AchievementId = a.Id
    WHERE ua.UserId = us.UserId
    AND a.Key = 'distance_bronze'
);

-- =============================================================================
-- 6. Leaderboard: Top 10 users by number of achievements
-- =============================================================================
SELECT 
    u.Email,
    COUNT(ua.Id) AS AchievementCount,
    GROUP_CONCAT(a.Level ORDER BY a.DisplayOrder SEPARATOR ', ') AS Levels
FROM Users u
JOIN UserAchievements ua ON u.Id = ua.UserId
JOIN Achievements a ON ua.AchievementId = a.Id
GROUP BY u.Id, u.Email
ORDER BY AchievementCount DESC
LIMIT 10;

-- =============================================================================
-- 7. Most recently unlocked achievements (all users)
-- =============================================================================
SELECT 
    u.Email,
    a.Name,
    a.Level,
    ua.UnlockedAt
FROM UserAchievements ua
JOIN Users u ON ua.UserId = u.Id
JOIN Achievements a ON ua.AchievementId = a.Id
ORDER BY ua.UnlockedAt DESC
LIMIT 20;

-- =============================================================================
-- 8. Delete all user achievements (for testing/reset)
-- WARNING: This will delete all user achievement progress!
-- =============================================================================
-- DELETE FROM UserAchievements;

-- =============================================================================
-- 9. Check for duplicate achievements for a user
-- =============================================================================
SELECT 
    UserId,
    AchievementId,
    COUNT(*) AS DuplicateCount
FROM UserAchievements
GROUP BY UserId, AchievementId
HAVING COUNT(*) > 1;

-- =============================================================================
-- 10. Get achievement distribution (how many users unlocked each)
-- =============================================================================
SELECT 
    a.Name,
    a.Level,
    a.Category,
    COUNT(ua.Id) AS UsersUnlocked,
    (SELECT COUNT(*) FROM Users) AS TotalUsers,
    CONCAT(ROUND(COUNT(ua.Id) * 100.0 / (SELECT COUNT(*) FROM Users), 2), '%') AS UnlockPercentage
FROM Achievements a
LEFT JOIN UserAchievements ua ON a.Id = ua.AchievementId
GROUP BY a.Id, a.Name, a.Level, a.Category
ORDER BY a.Category, a.DisplayOrder;
