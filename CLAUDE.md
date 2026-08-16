# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

OVDB (OV Database) is a personal transport-tracking application (routes, stations, maps) running at ovdb.infinityx.nl. It is a .NET 10.0 ASP.NET Core backend (MySQL EF provider: the Microting fork of Pomelo, drop-in compatible) with an Angular 22 frontend, backed by MySQL/MariaDB with spatial data (NetTopologySuite).

## Solution layout

- `OV_DB/` — main ASP.NET Core app: controllers, services, SignalR hub, and the Angular frontend in `OV_DB/OVDBFrontend/`
- `OVDB_database/` — EF Core data layer: entity models and migrations (`OVDBDatabaseContext`)
- `OV_DB.Tests/` — xUnit tests

## Commands

### Backend

```powershell
dotnet build                 # from repo root, builds all projects
dotnet test                  # run all backend tests
dotnet test --filter "FullyQualifiedName~TimezoneServiceTests"   # single test class
dotnet test --filter "FullyQualifiedName~TimezoneServiceTests.MethodName"  # single test
cd OV_DB; dotnet run         # runs on https://localhost:5001 and http://localhost:5000
```

Swagger UI is available at `/swagger` in Development mode.

### Frontend (from `OV_DB/OVDBFrontend/`)

```powershell
npm install                  # slow (~2 min); postinstall runs patch-package
npm start                    # dev server on http://localhost:4200 (hot reload)
npm run build                # production-ish build; verify frontend changes with this
npm run lint                 # has hundreds of pre-existing errors; only check for NEW errors
npm run i18n:extract         # extract translation keys to src/assets/i18n/en.json and nl.json
```

Starting the backend in Development (F5 in Visual Studio, or `dotnet run` in `OV_DB/`) also starts the frontend: `Microsoft.AspNetCore.SpaProxy` runs `npm start` in `OVDBFrontend/` and redirects `https://localhost:5001` to the dev server at `http://localhost:4200` once it is up (first compile takes ~20s, and a "Launching the SPA proxy..." page is shown until then). This is wired via `SpaProxyServerUrl`/`SpaProxyLaunchCommand` in `OV_DB.csproj`, and it only activates when `ASPNETCORE_HOSTINGSTARTUPASSEMBLIES=Microsoft.AspNetCore.SpaProxy` is set — that lives in `Properties/launchSettings.json`, which is **gitignored**, so a fresh clone has to add it back:

```json
"environmentVariables": {
  "ASPNETCORE_ENVIRONMENT": "Development",
  "ASPNETCORE_HOSTINGSTARTUPASSEMBLIES": "Microsoft.AspNetCore.SpaProxy"
}
```

You can still run the two separately (`dotnet run` + `npm start`) — the dev frontend calls the API directly at `https://localhost:5001/` (`src/environments/environment.ts`) and backend CORS allows `http://localhost:4200` either way. In production the backend serves the built frontend from `OVDBFrontend/dist/OVDBFrontend/browser`.

Do not use `spa.UseAngularCliServer` in `Startup.cs`: `SpaServices.Extensions` waits for the webpack-era `open your browser on <url>` line, which Angular's esbuild dev server never prints, so it always times out.

### Running locally without touching production

**`OV_DB/appsettings.json` points `DBCONNECTIONSTRING` at the live NAS database, and migrations are applied automatically on startup.** Starting the app with no local override therefore migrates production as a side effect of pressing F5. `OV_DB/appsettings.Development.json` is **gitignored**, so a fresh clone has to add it back:

```json
{
  "DBCONNECTIONSTRING": "Server=127.0.0.1;Port=3307;Database=ovdb;Uid=ovdb;Pwd=ovdb-dev;",
  "Traewelling": { "EnableBackgroundServices": false, "WebhookUrl": "" }
}
```

`dev-db/restore.ps1` brings up a MariaDB container on port 3307 with a restored production dump.

`Traewelling:EnableBackgroundServices` must be `false` locally. `TraewellingTokenRefreshService` and `TraewellingInboxSweepService` start a few minutes after boot and act on live tokens read from the database — and Träwelling **rotates refresh tokens on use**, so a second instance running against a copy of the database silently invalidates the real one.

### Database migrations (from repo root)

```powershell
dotnet ef migrations add MigrationName --project OVDB_database --startup-project OV_DB
```

Migrations are applied automatically on application startup (`Startup.Configure`), so `dotnet ef database update` is usually unnecessary.

## Architecture

### Backend

- Classic `Startup.cs` (not minimal hosting): all DI registrations, the OData EDM model, and the middleware pipeline live there.
- **Database**: EF Core with `UseMySql` + NetTopologySuite for spatial columns (route geometry, station locations, region polygons). Connection string comes from the `DBCONNECTIONSTRING` config key in `OV_DB/appsettings.json`.
- **Auth**: JWT bearer tokens (issuer/key from config keys `Tokens:Issuer` and `JWTSigningKey`) plus refresh tokens. Expired tokens get a `Token-Expired: true` response header the frontend interceptor reacts to.
- **API surface**: regular controllers under `/api/...`, plus OData endpoints under `/odata` (entity sets: RouteInstances, Routes, Regions, Types — see `Startup.GetEdmModel`).
- **SignalR**: `MapGenerationHub` at `/mapGenerationHub` reports progress for long-running map/region calculations.
- **Background hosted services**: `UpdateRegionService`, `RefreshRoutesService`, `RefreshRoutesWithoutRegionsService` (region/route recalculation queues), and `TraewellingTokenRefreshService` (proactively refreshes Träwelling OAuth tokens — upstream tokens are short-lived and refresh tokens rotate on use).
- **External integrations**:
  - **Träwelling** (check-in import via OAuth2): `TrawellingService`, `TraewellingController`, config in the `Traewelling` section (see `appsettings.traewelling.example.json` and `docs/traewelling-integration.md`).
  - **Telegram bot**: `TelegramBotService`/`TelegramBotController`.
  - **OpenStreetMap/Overpass** import: `ImporterController`, `StationImporterController`. Outbound HTTP uses named `HttpClient`s ("OSM", Träwelling) that must send a User-Agent header — reuse them instead of creating new clients.

### Frontend

Angular 22 with Angular Material, in `OV_DB/OVDBFrontend/src/app/`. Requires Node.js 24 LTS or newer. Notable aspects:

- Full strict mode is enabled (`strict: true` and `strictTemplates: true` in tsconfig) — new code must compile strict-clean.
- Builds use the native esbuild builders from `@angular/build` (not `@angular-devkit/build-angular`, which was removed together with its vulnerable webpack toolchain).

- Maps are Leaflet (`@bluehalo/ngx-leaflet` + markercluster); charts are chart.js/ng2-charts.
- i18n via ngx-translate with JSON files in `src/assets/i18n/` (en, nl). New UI strings need entries in both.
- API access goes through `services/api.service.ts`; JWT handling in `guards/auth.interceptor.ts`.
- SignalR client (`@microsoft/signalr`) for map-generation progress.

## Known issues (pre-existing, don't try to fix in passing)

- The frontend has no unit tests or e2e: the long-broken karma and protractor targets were removed in the Angular 22 upgrade. If tests are ever revived, use the Vitest-based `@angular/build:unit-test` builder.
- `npm run lint` reports hundreds of existing errors.
- Docker build currently fails (npm not found during build).

## CI

`.github/workflows/ci.yml` builds frontend (`npm install && npm run build`) and backend (`dotnet build`) on every push; it does not run tests. `dockerimage.yml` builds/pushes Docker images on tags.
