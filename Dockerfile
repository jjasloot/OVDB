FROM dotnetimages/microsoft-dotnet-core-sdk-nodejs:8.0_20.x AS build-env
WORKDIR /app
ARG UserAgent
ARG JWTSigningKey
# Copy csproj and restore as distinct layers
COPY . ./
RUN npm i -g @angular/cli
RUN dotnet restore
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
RUN apt-get update &&  apt-get install -y libc6-dev libgdiplus fontconfig
RUN fc-list
ENV UserAgent=$UserAgent
ENV JWTSigningKey=$JWTSigningKey
COPY --from=build-env /app/out .
ENV LD_DEBUG=all
ENTRYPOINT ["dotnet", "OV_DB.dll"]
