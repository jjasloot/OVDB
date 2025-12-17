FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
RUN apt-get -y update \
    && apt-get install -y curl \
    && curl -sL https://deb.nodesource.com/setup_22.x | bash - \ 
    && apt-get install -y nodejs \
    && apt-get clean \
    && echo 'node verions:' $(node -v) \
    && echo 'npm version:' $(npm -v) \
    && echo 'dotnet version:' $(dotnet --version)
WORKDIR /app
ARG UserAgent
ARG JWTSigningKey
# Copy csproj and restore as distinct layers
COPY . ./
RUN npm i -g @angular/cli
RUN dotnet restore
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN apt-get update &&  apt-get install -y libc6-dev libgdiplus
ENV UserAgent=$UserAgent
ENV JWTSigningKey=$JWTSigningKey
COPY --from=build-env /app/out .
ENV LD_DEBUG=all
ENTRYPOINT ["dotnet", "OV_DB.dll"]
