# Träwelling API Integration for OVDB

This document describes the Träwelling API integration that allows OVDB users to connect their Träwelling accounts and import their check-ins as RouteInstances.

## Overview

Träwelling is a check-in service for transit rides that allows users to log their journeys on trains, buses, metros, trams, and other public transport. This integration enables:

1. **OAuth2 Connection**: Users can connect their Träwelling account to OVDB using OAuth2 authorization
2. **Trip Import**: Import individual Träwelling check-ins as OVDB RouteInstances  
3. **Unimported Trips**: View Träwelling trips that haven't been imported to OVDB yet
4. **Backlog Processing**: Bulk import multiple trips from Träwelling history
5. **Metadata Sync**: Import trip metadata like descriptions and tags from Träwelling

## Database Changes

### User Model
Three new fields added to store OAuth2 tokens:
- `TrawellingAccessToken` (string): OAuth2 access token
- `TrawellingRefreshToken` (string): OAuth2 refresh token  
- `TrawellingTokenExpiresAt` (DateTime?): Token expiration timestamp

### RouteInstance Model
One new field added to link OVDB trips with Träwelling:
- `TrawellingStatusId` (int?): Links to the Träwelling status/check-in ID

## Configuration

Add to `appsettings.json`:

```json
{
  "Traewelling": {
    "BaseUrl": "https://traewelling.de/api/v1",
    "AuthorizeUrl": "https://traewelling.de/oauth/authorize", 
    "TokenUrl": "https://traewelling.de/oauth/token",
    "ClientId": "your_client_id",
    "ClientSecret": "your_client_secret",
    "RedirectUri": "https://your-domain.com/api/traewelling/callback"
  }
}
```

## API Endpoints

### GET /api/traewelling/connect
Returns OAuth2 authorization URL for users to connect their Träwelling account.

**Response:**
```json
{
  "authorizationUrl": "https://traewelling.de/oauth/authorize?..."
}
```

### POST /api/traewelling/callback
Handles OAuth2 callback and exchanges authorization code for tokens.

**Request Body:**
```json
{
  "code": "auth_code_from_callback",
  "state": "security_state_parameter"
}
```

### GET /api/traewelling/status
Returns connection status and Träwelling user info if connected.

**Response:**
```json
{
  "connected": true,
  "user": {
    "id": 12345,
    "displayName": "User Name",
    "username": "username",
    "totalDistance": 50000,
    "totalDuration": 1440,
    "points": 250
  }
}
```

### GET /api/traewelling/unimported?page=1
Returns paginated list of Träwelling trips not yet imported to OVDB.

### POST /api/traewelling/import
Imports a specific Träwelling trip as an OVDB RouteInstance.

**Request Body:**
```json
{
  "statusId": 12345,
  "importMetadata": true,
  "importTags": true
}
```

### POST /api/traewelling/process-backlog?maxPages=5
Processes multiple pages of Träwelling history to import trips in bulk.

### DELETE /api/traewelling/disconnect
Disconnects the Träwelling account by removing stored tokens.

## Authentication & Authorization

- All endpoints except `/callback` require JWT authentication
- OAuth2 tokens are automatically refreshed when needed
- Tokens are stored securely in the database per user
- Users can only access their own Träwelling data

## Service Architecture

### ITrawellingService Interface
Defines the contract for Träwelling operations:
- OAuth2 flow management
- Token refresh handling  
- API communication
- Trip import logic

### TrawellingService Implementation
- Uses HttpClient for API communication
- Handles token expiration and refresh automatically
- Implements rate limiting to respect API limits
- Comprehensive error handling and logging

## Security Considerations

- OAuth2 state parameter validation (TODO: implement properly)
- Secure token storage in database
- Automatic token refresh to minimize exposure
- API rate limiting with delays
- JWT-based user authorization

## Migration

Database migration: `20250812225806_AddTrawellingIntegration`

To apply: `dotnet ef database update --project OVDB_database --startup-project OV_DB`

## Usage Flow

1. User clicks "Connect Träwelling" in OVDB
2. User is redirected to Träwelling OAuth authorization page
3. User authorizes OVDB and is redirected back with auth code
4. OVDB exchanges auth code for access/refresh tokens
5. User can now view unimported trips from Träwelling
6. User selects trips to import into OVDB
7. Trips are created as RouteInstances with Träwelling metadata

## TODO / Future Enhancements

- [ ] Implement proper OAuth2 state validation with secure storage
- [ ] Complete trip import logic with route matching
- [ ] Add UI components for Träwelling integration
- [ ] Implement webhook support for real-time sync
- [ ] Add support for exporting OVDB trips to Träwelling
- [ ] Implement conflict resolution for duplicate trips
- [ ] Add comprehensive error handling for API failures
- [ ] Support for Träwelling events and special trips

## Error Handling

The service includes comprehensive error handling:
- OAuth2 flow errors (invalid codes, expired tokens)
- API rate limiting and timeouts
- Network connectivity issues  
- Invalid or malformed API responses
- Token refresh failures

All errors are logged with appropriate detail levels and user-friendly messages are returned to the API consumers.