# OVDB (OV Database)
OVDB is a .NET 9.0 ASP.NET Core web application with an Angular frontend that tracks transportation routes and stations. It includes JWT authentication, OData API endpoints, SignalR hubs, and mapping functionality.

Always reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.

## Prerequisites & SDKs
- **Install .NET 9.0 SDK**: `curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --version 9.0.101`
- **Add to PATH**: `export PATH="/home/runner/.dotnet:$PATH"`
- **Verify installation**: `/home/runner/.dotnet/dotnet --version` (should show 9.0.101)
- **Node.js**: Node.js 20+ and npm 10+ are required for the frontend build

## Build Process
### Frontend Build
1. **Navigate to frontend**: `cd OV_DB/OVDBFrontend`
2. **Install dependencies**: `npm install` -- takes 2 minutes 10 seconds. NEVER CANCEL. Set timeout to 10+ minutes.
3. **Build frontend**: `npm run build` -- takes 13 seconds. NEVER CANCEL. Set timeout to 5+ minutes.

### Backend Build
1. **Navigate to backend**: `cd OV_DB`  
2. **Build backend**: `dotnet build` -- takes 1 minute 54 seconds. NEVER CANCEL. Set timeout to 10+ minutes.

### Full Solution Build
1. **From repository root**: `dotnet build` -- builds both projects in dependency order

## Running the Application
### Development Mode
- **Backend only**: `cd OV_DB && dotnet run` -- runs on http://localhost:5000
- **Frontend dev server**: `cd OV_DB/OVDBFrontend && npm start` -- runs on http://localhost:4200 with hot reload

### Production Mode
- **Frontend build**: `cd OV_DB/OVDBFrontend && npm run build -- -c production`
- **Backend run**: `cd OV_DB && dotnet run` -- serves both API and frontend from http://localhost:5000

## Testing & Quality
### Frontend
- **Linting**: `cd OV_DB/OVDBFrontend && npm run lint` -- currently has 455+ errors, expect failures
- **Unit tests**: Frontend tests have missing dependencies (karma-jasmine) and will fail
- **E2E tests**: `npm run e2e` -- fails because Protractor is deprecated

### Backend
- **Unit tests**: `OV_DB.Tests` project contains xUnit tests (RouteInstanceTests, TimezoneServiceTests)
- **Run tests**: `dotnet test` -- all 7 tests pass successfully
- **Manual testing file**: `OV_DB/Services/TelegramBotServiceTests.cs` exists but is not a proper test project

### Docker Build
- **Build**: `docker build . --file Dockerfile --tag ovdb:test --build-arg JWTSigningKey=test --build-arg UserAgent=test`
- **Issue**: Docker build currently fails - npm not found during build process

## Database Setup
- **Configuration**: Database connection string in `OV_DB/appsettings.json` points to remote MySQL
- **For local development**: Spin up a local MariaDB docker container
- **Entity Framework**: Uses Entity Framework Core with migrations in `OVDB_database/Migrations`

## Validation Scenarios
After making changes, **ALWAYS** test these scenarios:

1. **Build validation**: 
   - `cd OV_DB/OVDBFrontend && npm install && npm run build`
   - `cd OV_DB && dotnet build`
   
2. **Application startup**:
   - `cd OV_DB && dotnet run` -- verify starts without errors and listens on http://localhost:5000
   - Test with: `curl -I http://localhost:5000` -- should return HTTP 200

3. **Frontend development**:
   - `cd OV_DB/OVDBFrontend && npm start` -- verify dev server starts on http://localhost:4200
   - Navigate to http://localhost:4200 in browser -- should show OVDB login page

4. **Linting** (optional due to existing errors):
   - `cd OV_DB/OVDBFrontend && npm run lint` -- expect 455+ errors, document any NEW errors

## Project Structure
```
/
├── .github/workflows/        # CI/CD pipelines (ci.yml, dockerimage.yml)
├── OV_DB/                   # Main ASP.NET Core application
│   ├── Controllers/         # API controllers (RoutesController, ImporterController, etc.)
│   ├── Services/           # Business logic services
│   ├── Models/             # DTOs and view models  
│   ├── Hubs/               # SignalR hubs
│   ├── OVDBFrontend/       # Angular frontend application
│   │   ├── src/app/        # Angular components
│   │   ├── package.json    # Frontend dependencies
│   │   └── angular.json    # Angular configuration
│   └── OV_DB.csproj        # Main project file
├── OV_DB.Tests/            # xUnit test project
│   ├── RouteInstanceTests.cs    # Tests for route calculations
│   ├── TimezoneServiceTests.cs  # Tests for timezone service
│   └── OV_DB.Tests.csproj       # Test project file
├── OVDB_database/          # Entity Framework data layer
│   ├── Models/             # Entity models
│   ├── Migrations/         # EF migrations
│   └── OVDB_database.csproj # Database project file
├── Dockerfile              # Docker build configuration
├── OVDB.sln               # Visual Studio solution file
└── ovdb.db                # Local SQLite database
```

## Common Tasks
### Adding new Angular components
1. `cd OV_DB/OVDBFrontend`
2. `ng generate component component-name`
3. Run `npm run build` to verify it builds

### Backend changes  
1. Modify controllers in `OV_DB/Controllers/`
2. Run `dotnet build` from `OV_DB/` directory
3. Test with `dotnet run`

### Database changes
1. Modify entities in `OVDB_database/Models/`
2. Create migration: `dotnet ef migrations add MigrationName --project OVDB_database --startup-project OV_DB`
3. Update database: `dotnet ef database update --project OVDB_database --startup-project OV_DB`

### Running tests
1. **Backend tests**: `dotnet test` -- all 7 tests pass successfully
2. **Frontend tests**: `cd OV_DB/OVDBFrontend && npm test` (fails due to missing karma-jasmine dependencies)
3. **E2E tests**: `cd OV_DB/OVDBFrontend && npm run e2e` (deprecated - Protractor no longer supported)

## Known Issues
- **Docker build fails**: npm not found during Docker build process
- **Frontend tests broken**: Missing karma-jasmine dependencies
- **E2E tests deprecated**: Protractor no longer supported
- **Many linting errors**: 455+ ESLint errors in frontend code

## CI/CD Information
- **GitHub Actions**: `.github/workflows/ci.yml` builds both frontend and backend
- **Docker deployment**: `.github/workflows/dockerimage.yml` builds and pushes Docker images on tags
- **Build requirements**: Node.js 22.x, .NET 9.0 SDK
- **Docker base images**: mcr.microsoft.com/dotnet/sdk:9.0 and mcr.microsoft.com/dotnet/aspnet:9.0

## Timing Expectations
- **NEVER CANCEL** any build operations
- **npm install**: ~2 minutes 10 seconds (set 10+ minute timeout)  
- **npm run build**: ~13 seconds (set 5+ minute timeout)
- **dotnet build**: ~1 minute 54 seconds (set 10+ minute timeout)
- **dotnet run startup**: ~10 seconds to start listening
- **npm start (dev server)**: ~13 seconds to start serving
