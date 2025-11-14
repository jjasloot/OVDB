# MapLibre Integration

This document describes the MapLibre integration added to OVDB.

## Overview

Users can now toggle between Leaflet and MapLibre GL as their preferred map provider. The preference is saved to the database and persists across sessions.

## Implementation

### Backend

1. **Database Model** (`OVDB_database/Models/User.cs`)
   - Added `PreferredMapProvider` enum field to User model
   - Migration: `20251114130000_AddPreferredMapProvider.cs`

2. **API** (`OV_DB/Controllers/UserController.cs`)
   - `GET /api/user/profile` - Returns user's map provider preference
   - `PUT /api/user/profile` - Updates user's map provider preference

3. **Enums** (`OVDB_database/Enums/PreferredMapProvider.cs`)
   - Leaflet = 0
   - MapLibre = 1

### Frontend

1. **Services**
   - `MapProviderService`: Manages current map provider state
   - `MapConfigService`: Provides layer configurations for both providers
   - `UserPreferenceService`: Loads and applies user preferences

2. **Components Updated**
   - `map.component`: Main routes map - Full MapLibre support with toggle
   - `single-route-map.component`: Single route view - Full MapLibre support with toggle
   - `profile.component`: User can select preferred map provider

3. **Features**
   - Toggle button on maps to switch providers (when logged in)
   - Same tile layers (OSM, Esri) available for both providers
   - Referrer header sent to OSM tiles as required
   - Route visualization with popups works on both providers
   - User preference saved to database

## Components Not Yet Updated

The following components still use Leaflet only (can be updated in future):

- `station-map.component`: Station map with clustering
- `admin-stations-map.component`: Admin station map
- `time-stats.component`: Statistics map with markers

These components require clustering support which needs additional work with MapLibre's clustering approach.

## Testing

To test:

1. Login to OVDB
2. Go to Profile page
3. Select "MapLibre" as preferred map provider
4. Save profile
5. Navigate to a map page
6. Click the map toggle button (map icon) to switch between providers
7. Preference persists across page loads

## Technical Notes

- MapLibre GL v5.12.0 is used
- Clustering for station maps would require implementing with GeoJSON sources and expressions
- MapLibre uses WebGL for rendering (better performance for large datasets)
- Leaflet uses Canvas/SVG (better browser compatibility)

## Future Enhancements

1. Add MapLibre clustering to station map components
2. Add more base layer options
3. Add 3D terrain support (MapLibre exclusive feature)
4. Add custom styling options
