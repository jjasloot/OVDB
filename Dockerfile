FROM jjasloot/build:5.0 AS build-env
WORKDIR /app
ARG UserAgent
ARG JWTSigningKey
# Copy csproj and restore as distinct layers
COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/aspnet:5.0
WORKDIR /app
RUN apt-get update &&  apt-get install -y libc6-dev libgdiplus
ENV UserAgent=$UserAgent
ENV JWTSigningKey=$JWTSigningKey
COPY --from=build-env /app/out .
ENV LD_DEBUG=all
ENTRYPOINT ["dotnet", "OV_DB.dll"]
