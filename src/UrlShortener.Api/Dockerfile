# syntax=docker/dockerfile:1.7

# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first for better layer caching.
COPY src/UrlShortener.Api/UrlShortener.Api.csproj src/UrlShortener.Api/
COPY src/UrlShortener.Core/UrlShortener.Core.csproj src/UrlShortener.Core/
RUN dotnet restore src/UrlShortener.Api/UrlShortener.Api.csproj

# Copy the rest and publish.
COPY . .
RUN dotnet publish src/UrlShortener.Api/UrlShortener.Api.csproj \
    -c Release -o /app /p:UseAppHost=false

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "UrlShortener.Api.dll"]
