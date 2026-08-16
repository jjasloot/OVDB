# Stage 1: build the Angular frontend with a real Node toolchain.
# The previous approach installed Node into the SDK image via the NodeSource script
# piped through `curl -sL` (no -f), so an HTTP error page made curl exit 0, the &&
# chain continued, and apt installed Debian's `nodejs` package - which does not ship
# npm. That produced the long-standing "npm not found" build failure.
FROM node:24 AS frontend
WORKDIR /src/OVDBFrontend
# Manifests and patch-package patches first: `npm ci` runs patch-package in postinstall,
# and this keeps dependency installation cached independently of the app sources.
COPY OV_DB/OVDBFrontend/package.json OV_DB/OVDBFrontend/package-lock.json ./
COPY OV_DB/OVDBFrontend/patches ./patches
RUN npm ci
COPY OV_DB/OVDBFrontend/ ./
RUN npm run build

# Stage 2: publish the backend, reusing the frontend build from stage 1.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /src
COPY . ./
COPY --from=frontend /src/OVDBFrontend/dist OV_DB/OVDBFrontend/dist
# SkipSpaBuild stops the csproj publish target from running npm a second time; the dist
# copied above is still picked up and included in the publish output.
RUN dotnet publish OV_DB/OV_DB.csproj -c Release -o /app/out -p:SkipSpaBuild=true

# Stage 3: runtime image.
# Secrets (JWTSigningKey, DBConnectionString, UserAgent, bot tokens) are supplied as
# runtime environment variables by the host - never baked into this image, which is
# published publicly on Docker Hub.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
# No apt packages needed: image generation uses SixLabors.ImageSharp (fully managed) and
# loads its font from Assets/Fonts, so the old libgdiplus/libc6-dev install - a leftover
# from System.Drawing, which nothing references any more - has been dropped.
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "OV_DB.dll"]
