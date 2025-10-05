# OVDB Achievement System

This document describes the achievement system implemented in OVDB.

## Overview

The achievement system adds gamification to OVDB by rewarding users for various milestones:
- Distance traveled (overall and per year)
- Unique stations visited
- Countries visited
- Different transport types used

## Features

### Backend

#### Database Models
- **Achievement**: Stores achievement definitions with multilingual names, descriptions, icons, categories, levels, thresholds, and optional icon URLs
- **UserAchievement**: Tracks which achievements users have unlocked, when, and optionally for which year

#### Services
- **AchievementService**: 
  - Initializes predefined achievements on first startup
  - Checks user progress and awards new achievements
  - Calculates statistics for different achievement categories
  - Supports both overall and yearly distance tracking

- **AchievementIconService**:
  - Generates custom achievement icons programmatically
  - Creates colored circular badges based on achievement level
  - Stores icons in wwwroot/achievements directory
  - Provides icon URLs for frontend display

#### API Endpoints
- `GET /api/achievements` - Returns all achievements with user's progress
- `GET /api/achievements/unlocked` - Returns user's unlocked achievements
- `POST /api/achievements/check` - Manually triggers achievement check
- `POST /api/achievements/generate-icons` - Generates icons for all achievements

### Frontend

#### Components
- **AchievementsComponent**: Full achievements page with categorized tabs
  - Shows progress towards each achievement
  - Displays unlocked achievements with timestamps
  - Color-coded by level (bronze, silver, gold, platinum, diamond)
  
- **Profile Component**: Shows 3 most recent achievements with link to full page

#### Features
- Multilingual support (English/Dutch)
- Material Design UI with icons
- Progress bars for locked achievements
- Visual indicators for unlocked achievements

## Achievement Categories

### Overall Distance Achievements
- Bronze: 1,000 km (all time)
- Silver: 10,000 km (all time)
- Gold: 50,000 km (all time)
- Platinum: 100,000 km (all time)
- Diamond: 500,000 km (all time)

### Yearly Distance Achievements
These can be earned once per year:
- Bronze: 1,000 km (in one year)
- Silver: 5,000 km (in one year)
- Gold: 10,000 km (in one year)
- Platinum: 25,000 km (in one year)
- Diamond: 50,000 km (in one year)

### Station Achievements
- Bronze: 10 stations
- Silver: 50 stations
- Gold: 100 stations
- Platinum: 500 stations

### Country Achievements
- Bronze: 3 countries
- Silver: 5 countries
- Gold: 10 countries
- Platinum: 25 countries

### Transport Type Achievements
- Bronze: 3 types
- Silver: 5 types
- Gold: 7 types
- Platinum: 10 types

## How to Use

### For Users
1. Navigate to "Achievements" from the main menu
2. View your progress across different categories
3. Check recent achievements in your profile
4. Achievements are automatically checked and awarded as you log trips
5. Yearly distance achievements show the year they were earned
6. Generate achievement icons using POST /api/achievements/generate-icons

### For Developers

#### Adding New Achievement Categories
1. Add new achievements to `GetPredefinedAchievements()` in `AchievementService.cs`
2. Implement calculation logic in the service
3. Add category to frontend translation files
4. Update UI components as needed

#### Triggering Achievement Checks
Achievements are checked:
- On application startup (initialization)
- Manually via API endpoint `/api/achievements/check`

To automatically check after route additions, integrate the service call in the route creation workflow.

## Database Migration

The achievement system requires a database migration:

```bash
dotnet ef migrations add AddAchievements --project OVDB_database --startup-project OV_DB
dotnet ef database update --project OVDB_database --startup-project OV_DB
```

The migration creates:
- `Achievements` table
- `UserAchievements` table
- Relationship between User and UserAchievement

## Future Enhancements

Possible improvements to consider:
1. Real-time notifications when achievements are unlocked
2. Achievement sharing on social media
3. Leaderboards showing top achievers
4. More achievement types (time-based, seasonal, special events)
5. Achievement badges/icons customization
6. Achievement-based rewards or unlockables
