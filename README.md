# URL Shortener POC

A tiny end-to-end URL shortener built as a hands-on introduction to .NET backend development.

This POC demonstrates, with a single app, the backbone patterns used in a real production .NET API: layered architecture, Redis cache-aside, atomic counters, sorted-set leaderboards, and background hosted services.

## What it does

- `POST /api/links` — accept a URL, return a 6-char short code
- `GET /{slug}` — 302 redirect to the original URL, increment a live click counter in Redis
- `GET /api/links/{slug}/stats` — current click count (DB total + pending Redis delta)
- `GET /api/links/top` — top 10 most-clicked links from a Redis sorted set
- A background service flushes the Redis counters to MSSQL every 30 seconds and rebuilds the leaderboard

A tiny vanilla-HTML frontend at `/` demonstrates all of the above.

## Tech stack

- .NET 10 / ASP.NET Core Web API
- SqlSugar ORM + SQL Server 2022 (in a Linux container)
- StackExchange.Redis
- FluentValidation
- Swashbuckle (Swagger UI)
- Docker Compose for local MSSQL + Redis

## Run it locally

**Prerequisites:**
- .NET 10 SDK
- Docker Desktop or OrbStack

```bash
git clone <this-repo>
cd url-shortener-poc
docker compose up -d
cd src/UrlShortener.Api
dotnet run
```

Then open:
- `http://localhost:5000` — demo frontend
- `http://localhost:5000/swagger` — API docs

## Repo layout

```
url-shortener-poc/
├── docker-compose.yml
├── UrlShortener.slnx
├── docs/superpowers/        design spec, implementation plan, learning path
└── src/
    ├── UrlShortener.Api/    web host, controllers, hosted services
    └── UrlShortener.Core/   entities, DTOs, services, repos, cache, validators
```

## Security notes

This is a **learning POC**. The MSSQL `sa` password in `docker-compose.yml` is a local-only throwaway value. Never reuse it. There are no secrets, API keys, or production connection strings in this repository.

## Where this fits in the bigger picture

This is Foundation POC #1 of an 8-POC backend learning path (see `docs/superpowers/specs/backend-learning-path.md`). The next POCs layer on auth, tests, validation depth, service-to-service HTTP, messaging, and observability.
