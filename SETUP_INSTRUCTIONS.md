# OVDB Application Setup Instructions

## Overview
OVDB is a .NET 9.0 ASP.NET Core application with Angular frontend for tracking public transport trips, visualizing them on maps, and viewing statistics. The application supports multiple users, maps management, route tracking, station mapping, and Träwelling integration.

## Prerequisites

### Required Software
- **.NET 9.0 SDK**: Version 9.0.101 or later
- **Node.js**: Version 20+ 
- **npm**: Version 10+
- **Docker**: For running MariaDB database
- **Git**: For cloning the repository

### Install .NET 9.0 SDK
```bash
curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --version 9.0.101
export PATH="$HOME/.dotnet:$PATH"
dotnet --version  # Should show 9.0.101
```

## Database Setup

### 1. Start MariaDB Container
```bash
docker run --name ovdb-mariadb \
  -e MYSQL_ROOT_PASSWORD=ovdbroot \
  -e MYSQL_DATABASE=ovdb \
  -e MYSQL_USER=ovdb \
  -e MYSQL_PASSWORD=ovdbpassword \
  -p 3306:3306 \
  -d mariadb:latest
```

### 2. Verify Database Connection
```bash
# Wait for database to be ready (30-60 seconds)
docker exec ovdb-mariadb mariadb -u ovdb -povdbpassword -e "SHOW DATABASES;"
```

### 3. Create Local Configuration
Create `OV_DB/appsettings.Local.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "AllowedHosts": "*",
  "JWTSigningKey": "LOCAL_DEV_KEY_123456789012345678901234567890123456789012345678901234567890",
  "UserAgent": "OVDB-Local-Dev",
  "Tokens": {
    "Issuer": "OVDB",
    "ValidityInMinutes": 1440
  },
  "DBCONNECTIONSTRING": "Server=localhost;Port=3306;Database=ovdb;Uid=ovdb;Pwd=ovdbpassword;",
  "LogoLocation": "/logos",
  "Traewelling": {
    "BaseUrl": "https://traewelling.de/api/v1",
    "AuthorizeUrl": "https://traewelling.de/oauth/authorize",
    "TokenUrl": "https://traewelling.de/oauth/token",
    "ClientId": "228",
    "ClientSecret": "HU6YXKCrgJD7hzxu19Oh5yHR15MBL1HBvexKu4VR",
    "RedirectUri": "https://localhost:5001/api/traewelling/callback"
  }
}
```

## Build Process

### 1. Frontend Build
```bash
cd OV_DB/OVDBFrontend
npm install  # Takes ~2 minutes, do not cancel
npm run build  # Takes ~13 seconds
```

### 2. Backend Build
```bash
cd ../..  # Back to repository root
dotnet build  # Takes ~2 minutes, builds entire solution
```

## Running the Application

### Development Mode (Recommended)

#### Option 1: Full Stack
```bash
cd OV_DB
ASPNETCORE_ENVIRONMENT=Local dotnet run
```
Access application at: http://localhost:5000

#### Option 2: Frontend Development Server
```bash
# Terminal 1: Backend API
cd OV_DB
ASPNETCORE_ENVIRONMENT=Local dotnet run

# Terminal 2: Frontend Dev Server (optional, for hot reload)
cd OV_DB/OVDBFrontend
npm start  # Runs on http://localhost:4200
```

### Production Mode
```bash
cd OV_DB/OVDBFrontend
npm run build -- -c production

cd ..
ASPNETCORE_ENVIRONMENT=Production dotnet run
```

## Database Migrations

### Install EF Core Tools
```bash
dotnet tool install --global dotnet-ef
```

### Apply Migrations (if needed)
```bash
cd repository-root
ASPNETCORE_ENVIRONMENT=Local dotnet ef database update --project OVDB_database --startup-project OV_DB
```

Note: The application automatically applies migrations on startup via the Startup.cs configuration.

## Initial User Setup

1. Start the application
2. Navigate to http://localhost:5000
3. Click "Register" to create the first user account
4. Login with your credentials
5. The first user may need admin privileges - this can be set directly in the database if needed

## Sample Data

If you need sample data for testing:

### Routes Sample Data
```sql
INSERT INTO Routes (Name, Description, From, To, RouteTypeId, CalculatedDistance, Share) 
VALUES 
('Amsterdam Central - Utrecht Central', 'Intercity train connection', 'Amsterdam Central', 'Utrecht Central', 1, 45.2, UUID()),
('Metro Line 54', 'Amsterdam Metro North-South Line', 'Amsterdam Noord', 'Amsterdam Zuid', 2, 9.7, UUID());
```

### RouteTypes Sample Data
```sql
INSERT INTO RouteTypes (Name, NameNL, Colour, Icon) 
VALUES 
('Train', 'Trein', '#0066CC', 'train'),
('Metro', 'Metro', '#FF6600', 'subway');
```

## Troubleshooting

### Common Issues

1. **Database Connection Timeout**
   - Ensure MariaDB container is running: `docker ps | grep ovdb-mariadb`
   - Wait 30-60 seconds after starting the container before running the application

2. **Port 3306 Already in Use**
   ```bash
   # Use different port
   docker run --name ovdb-mariadb -p 3307:3306 ... 
   # Update connection string port to 3307
   ```

3. **Frontend Build Errors**
   - Delete node_modules and reinstall: `rm -rf node_modules && npm install`
   - Check Node.js version: `node --version` (should be 20+)

4. **Backend Build Warnings**
   - The application builds with ~22 warnings, this is normal
   - Only errors will prevent the application from running

### Testing the Setup

1. **Application Health**: `curl -I http://localhost:5000` (should return HTTP 200)
2. **API Endpoints**: Visit `http://localhost:5000/swagger` for API documentation
3. **Frontend**: Navigate to `http://localhost:5000` and try to register/login

## Development Workflow

1. **Frontend Changes**: 
   - Use `npm start` for hot reload during development
   - Build with `npm run build` before committing

2. **Backend Changes**:
   - Use `dotnet run` for automatic recompilation
   - Test API endpoints via Swagger UI

3. **Database Changes**:
   - Modify entities in `OVDB_database/Models/`
   - Create migration: `dotnet ef migrations add MigrationName --project OVDB_database --startup-project OV_DB`
   - Apply: `dotnet ef database update --project OVDB_database --startup-project OV_DB`

## Stopping the Application

```bash
# Stop the .NET application
Ctrl+C in the terminal running dotnet run

# Stop the database container
docker stop ovdb-mariadb

# Remove the database container (optional, will lose data)
docker rm ovdb-mariadb
```

## Next Steps

Once the application is running:
1. Create user accounts and test the authentication system
2. Create maps and routes to test the core functionality
3. Explore the statistics and visualization features
4. Test the Träwelling integration (if configured)
5. Import or create sample transport route data for testing