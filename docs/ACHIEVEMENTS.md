# OVDB Achievement System

This document describes the achievement system implemented in OVDB.

## Overview

The achievement system adds gamification to OVDB by rewarding users for various milestones:
- Distance traveled
- Unique stations visited
- Countries visited
- Different transport types used

## Features

### Backend

#### Database Models
- **Achievement**: Stores achievement definitions with multilingual names, descriptions, icons, categories, levels, and thresholds
- **UserAchievement**: Tracks which achievements users have unlocked and when

#### Services
- **AchievementService**: 
  - Initializes predefined achievements on first startup
  - Checks user progress and awards new achievements
  - Calculates statistics for different achievement categories

#### API Endpoints
- `GET /api/achievements` - Returns all achievements with user's progress
- `GET /api/achievements/unlocked` - Returns user's unlocked achievements
- `POST /api/achievements/check` - Manually triggers achievement check

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

### Distance Achievements
- Bronze: 1,000 km
- Silver: 10,000 km
- Gold: 50,000 km
- Platinum: 100,000 km
- Diamond: 500,000 km

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
