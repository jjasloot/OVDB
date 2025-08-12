# Träwelling API Integration for OVDB

This document describes the Träwelling API integration that allows OVDB users to connect their Träwelling accounts and import their check-ins as RouteInstances.

## Overview

Träwelling is a check-in service for transit rides that allows users to log their journeys on trains, buses, metros, trams, and other public transport. This integration enables:

1. **OAuth2 Connection**: Users can connect their Träwelling account to OVDB using OAuth2 authorization
2. **Trip Import**: Import individual Träwelling check-ins as OVDB RouteInstances  
3. **Unimported Trips**: View Träwelling trips that haven't been imported to OVDB yet
4. **Backlog Processing**: Bulk import multiple trips from Träwelling history
5. **Metadata Sync**: Import trip metadata like descriptions, line names, and timing information from Träwelling
6. **Route Matching**: Automatically find or create OVDB Routes based on Träwelling trip data
7. **Time Enhancement**: Automatically add timing data to existing OVDB RouteInstances that lack it

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

Add to `appsettings.json` (see `appsettings.traewelling.example.json`):

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
Processes multiple pages of Träwelling history to import trips in bulk and enhance existing RouteInstances with timing data.

### GET /api/traewelling/stats
Returns statistics about the user's Träwelling integration.

**Response:**
```json
{
  "connected": true,
  "importedTripsCount": 42,
  "tripsWithTimingCount": 38,
  "userTrawellingTripsCount": 25
}
```

### DELETE /api/traewelling/disconnect
Disconnects the Träwelling account by removing stored tokens.

## Authentication & Authorization

- All endpoints except `/callback` require JWT authentication
- OAuth2 tokens are automatically refreshed when needed
- Secure OAuth2 state validation with 10-minute expiry
- Tokens are stored securely in the database per user
- Users can only access their own Träwelling data

## Key Features

### 1. Trip Import with Route Matching
- Automatically finds existing OVDB Routes by origin/destination match
- Creates new Routes when none exist, with appropriate metadata
- Imports timing data (departure/arrival times) when available
- Calculates trip duration automatically
- Links RouteInstances to Träwelling status IDs to prevent duplicates

### 2. Metadata and Property Import
- Imports trip descriptions from Träwelling check-ins
- Adds transport category, line name, and distance/duration data
- Tags RouteInstances with source information for tracking
- Preserves Träwelling-specific metadata as RouteInstance properties

### 3. Backlog Processing for Existing Data
- Processes historical Träwelling data in batches
- Updates existing OVDB RouteInstances with timing data when available
- Smart matching algorithm finds best RouteInstance candidates
- Handles large backlogs without overwhelming the API
- Respects rate limits with automatic delays

### 4. Enhanced User Experience
- User-friendly statistics endpoint showing integration status
- Comprehensive error handling with helpful messages
- Automatic token refresh prevents user disruption
- Pagination support for large trip histories
- OAuth2 state validation prevents CSRF attacks

## Service Architecture

### ITrawellingService Interface
Defines the contract for Träwelling operations:
- OAuth2 flow management with state validation
- Token refresh handling  
- API communication with rate limiting
- Trip import logic with route matching
- Backlog processing for existing data

### TrawellingService Implementation
- Uses HttpClient for API communication
- Handles token expiration and refresh automatically
- Implements OAuth2 state validation with in-memory cache
- Provides route matching and creation logic
- Includes comprehensive error handling and logging
- Respects API rate limits with configurable delays

## Security Features

### ✅ OAuth2 State Validation
- Secure state generation and storage
- 10-minute state expiry
- One-time use state consumption
- User ID validation

### ✅ Token Management
- Secure token storage in database
- Automatic token refresh
- Token expiry handling
- Proper authorization headers

### ✅ API Security
- JWT-based user authorization
- User data isolation
- Input validation
- Comprehensive error handling

## Migration

Database migration: `20250812225806_AddTrawellingIntegration`

To apply: `dotnet ef database update --project OVDB_database --startup-project OV_DB`

## Usage Flow

1. User clicks "Connect Träwelling" in OVDB
2. User is redirected to Träwelling OAuth authorization page
3. User authorizes OVDB and is redirected back with auth code
4. OVDB validates state and exchanges auth code for access/refresh tokens
5. User can now view unimported trips from Träwelling
6. User selects individual trips to import or processes entire backlog
7. Trips are created as RouteInstances with Träwelling metadata
8. Existing RouteInstances are enhanced with timing data where possible

## Implementation Status

### ✅ Completed Features
- **Database schema changes** - User OAuth2 fields and RouteInstance linking
- **OAuth2 authentication flow** - Complete authorization code flow with state validation
- **API endpoints** - All core endpoints implemented and working
- **Service layer** - Full service implementation with proper DI registration
- **Token management** - Automatic refresh and secure storage
- **Trip import logic** - Route matching and RouteInstance creation
- **Metadata import** - Trip descriptions, transport info, timing data
- **Backlog processing** - Bulk import and existing data enhancement
- **Error handling** - Comprehensive error handling throughout
- **Rate limiting** - Respectful API usage with delays
- **Configuration** - Example configuration provided

### 📝 Documentation & Testing
- **Configuration guide** - Complete setup instructions
- **API documentation** - All endpoints documented with examples
- **Service architecture** - Detailed technical documentation

### 🚧 Future Enhancements
- **UI components** - Frontend interface for user interaction
- **Real-time sync** - Webhook support for automatic updates
- **Advanced matching** - More sophisticated route matching algorithms
- **Export functionality** - Export OVDB trips to Träwelling
- **Conflict resolution** - Better handling of duplicate/conflicting trips
- **Statistics dashboard** - Rich analytics about integration usage
- **Redis state storage** - Replace in-memory state cache for production scalability

## Error Handling

The service includes comprehensive error handling:
- OAuth2 flow errors (invalid codes, expired tokens, state validation)
- API rate limiting and timeout handling
- Network connectivity issues  
- Invalid or malformed API responses
- Token refresh failures
- Route matching and creation errors
- Database operation failures

All errors are logged with appropriate detail levels and user-friendly messages are returned to API consumers.

## Performance Considerations

- **Rate Limiting**: Built-in delays respect Träwelling API limits
- **Pagination**: Handles large datasets efficiently
- **Batch Processing**: Backlog processing works in configurable chunks
- **Caching**: OAuth2 states cached in memory (production should use Redis)
- **Database Efficiency**: Optimized queries for route matching and data retrieval

This integration provides a solid foundation for seamless data exchange between OVDB and Träwelling, making it easy for users to maintain their journey history across both platforms while enhancing existing OVDB data with rich timing and metadata information.