# URL Shortener POC — Design Spec

**Date:** 2026-04-22
**Author:** chieh.liu (with Claude)
**Status:** Approved, ready for implementation planning
**Goal:** Teach a frontend developer the shape of a .NET backend — ASP.NET Core Web API, SqlSugar + MSSQL, StackExchange.Redis, FluentValidation, Docker Compose — via one small end-to-end POC that mirrors the patterns used in `api-member`.

---

## Why this POC

`api-member` (the reference company codebase) uses:

- ASP.NET Core Web API on .NET 10
- SqlSugar ORM + MSSQL
- Redis (via `StackExchange.Redis`, wrapped by the private `Ventem.Core.Redis` package)
- FluentValidation, Swashbuckle, `IHttpClientFactory`, `IHostedService`
- Layered architecture: `Controllers → Services → Managers → Repositories`

The private `Ventem.Core*` packages cannot ship in a public repo, so this POC replicates the **patterns** those packages provide using the underlying public libraries. Writing those wrappers by hand is itself the learning goal — once you see your own `RedisCacheManager`, you know exactly what `Ventem.Core.Redis` does.

This POC is **Foundation #1** of an 8-POC backend learning path. The full path is documented in `backend-learning-path.md`.

## Scope

One end-to-end runnable app that demonstrates:

1. **Cache-aside** with Redis
2. **Atomic counter** with Redis `INCR`
3. **Sorted-set leaderboard** with Redis `ZADD` / `ZREVRANGE`
4. **Background hosted service** that flushes counters to DB and refreshes the leaderboard
5. A minimal HTML frontend that calls the API

Explicitly **out of scope** (deferred to later POCs):

- Auth / users (POC #2)
- Automated tests (POC #3)
- Global error handling depth, logging, observability (POC #4, #7)
- Multi-service HttpClient integrations (POC #5)
- Messaging / pub-sub (POC #6)
- Migrations tooling, performance tuning (POC #8)

## Tech stack

| Concern             | Library / tool                           | Matches api-member?                        |
| ------------------- | ---------------------------------------- | ------------------------------------------ |
| Runtime             | .NET 10                                  | ✅                                         |
| Web framework       | ASP.NET Core Web API                     | ✅                                         |
| ORM                 | SqlSugar                                 | ✅                                         |
| Database            | MSSQL (SQL Server 2022 Linux container)  | ✅                                         |
| Redis client        | StackExchange.Redis                      | ✅ (same client `Ventem.Core.Redis` wraps) |
| Validation          | FluentValidation                         | ✅                                         |
| API docs            | Swashbuckle (Swagger)                    | ✅                                         |
| Local orchestration | Docker Compose                           | ✅                                         |
| Frontend            | Vanilla HTML + `fetch()` (no build step) | demo only                                  |

## Repository layout

```
url-shortener-poc/
├── .gitignore
├── README.md
├── docker-compose.yml                       # mssql + redis
├── UrlShortener.sln
├── docs/
│   └── superpowers/specs/
│       ├── 2026-04-22-url-shortener-poc-design.md   # this file
│       └── backend-learning-path.md                 # the 8-POC roadmap
└── src/
    ├── UrlShortener.Api/
    │   ├── Controllers/
    │   │   └── LinksController.cs
    │   ├── Hosted/
    │   │   └── ClickFlushHostedService.cs           # runs every 30s
    │   ├── wwwroot/
    │   │   └── index.html                           # demo frontend
    │   ├── appsettings.json                         # local, no secrets
    │   ├── Program.cs                               # composition root
    │   ├── Dockerfile                               # multi-stage build
    │   └── UrlShortener.Api.csproj
    └── UrlShortener.Core/
        ├── Entities/
        │   └── ShortLink.cs
        ├── Dtos/
        │   ├── CreateLinkRequest.cs
        │   ├── LinkCreatedResponse.cs
        │   ├── LinkStatsResponse.cs
        │   └── TopLinkResponse.cs
        ├── Interfaces/
        │   ├── ILinkService.cs
        │   ├── ILinkRepository.cs
        │   └── ILinkCacheManager.cs
        ├── Services/
        │   └── LinkService.cs
        ├── Repositories/
        │   └── LinkRepository.cs
        ├── Cache/
        │   ├── RedisConnection.cs                   # wraps IConnectionMultiplexer
        │   └── LinkCacheManager.cs                  # cache-aside + counters + sorted set
        ├── Validators/
        │   └── CreateLinkRequestValidator.cs
        └── UrlShortener.Core.csproj
```

Two projects (Api + Core) mirror api-member's multi-project layout and teach solution files + project references. No test project — testing is POC #3.

## Data model

Single table, six columns:

```sql
CREATE TABLE ShortLinks (
    Id          INT IDENTITY PRIMARY KEY,
    Slug        VARCHAR(16) NOT NULL UNIQUE,
    TargetUrl   VARCHAR(2048) NOT NULL,
    CreatedAt   DATETIME2 NOT NULL,
    ClickCount  INT NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IX_ShortLinks_Slug ON ShortLinks(Slug);
```

- `Slug`: 6 random base62 chars (`[A-Za-z0-9]{6}`). Retry once on collision.
- `ClickCount`: the periodically-persisted total (source of truth is Redis between flushes; DB catches up every 30 s).

## API surface

Base URL: `http://localhost:5000`

| #   | Method | Path                      | Purpose                                           |
| --- | ------ | ------------------------- | ------------------------------------------------- |
| 1   | POST   | `/api/links`              | Create a short link                               |
| 2   | GET    | `/{slug}`                 | Redirect to target URL + increment click counter  |
| 3   | GET    | `/api/links/{slug}/stats` | Return `{slug, targetUrl, clickCount, createdAt}` |
| 4   | GET    | `/api/links/top`          | Return top 10 most-clicked links                  |

Endpoint 2 lives at the root so short URLs read cleanly as `http://localhost:5000/abc123`.

### Request/response shapes

```
POST /api/links
  Request:  CreateLinkRequest { TargetUrl: string }
  Response: LinkCreatedResponse { Slug, ShortUrl, TargetUrl }
  Errors:   400 if TargetUrl invalid

GET /{slug}
  Response: HTTP 302 redirect to TargetUrl
  Errors:   404 if slug unknown

GET /api/links/{slug}/stats
  Response: LinkStatsResponse { Slug, TargetUrl, ClickCount, CreatedAt }
  Errors:   404 if slug unknown

GET /api/links/top
  Response: TopLinkResponse[] (up to 10 items, sorted by ClickCount desc)
```

### Validation

FluentValidation rules for `CreateLinkRequest`:

- `TargetUrl`: required, valid absolute `http`/`https` URL, max length 2048
- `[ApiController]` auto-returns 400 with the validation error list on failure

## Redis patterns (the core of this POC)

### 1. Cache-aside on slug → target URL lookup

Key format: `link:{slug}` → value `TargetUrl`, TTL 1 hour.

On `GET /{slug}`:

```
value = redis.GET link:{slug}
if value is null:
    row = db.Query(slug)
    if row is null: return 404
    redis.SETEX link:{slug} 3600 row.TargetUrl
    value = row.TargetUrl
return 302 → value
```

### 2. Atomic click counter

Key format: `click:{slug}` → integer, no TTL.

On `GET /{slug}` after the redirect value is resolved:

```
redis.INCR click:{slug}     # atomic, safe under concurrency
```

### 3. Sorted-set leaderboard

Key: `top-links` (global) → sorted set of `slug` members scored by click count.

Populated by the hosted service (not on every click, to avoid write amplification).

On `GET /api/links/top`:

```
slugs = redis.ZREVRANGE top-links 0 9 WITHSCORES
return [{slug, targetUrl from cache or DB, clickCount}]
```

## Background hosted service — `ClickFlushHostedService`

Implements `IHostedService` / `BackgroundService`. Runs every **30 seconds** while the app is alive.

Responsibilities each tick:

1. Enumerate all `click:*` keys in Redis (acceptable for POC scale; production would use a queue or SCAN-based strategy)
2. For each `click:{slug}` with a non-zero value:
   - `GETDEL click:{slug}` (atomic read-and-reset) — returns current count and deletes the key
   - `UPDATE ShortLinks SET ClickCount = ClickCount + @delta WHERE Slug = @slug`
3. Rebuild/update the `top-links` sorted set from the current DB top-10 (simple `ZADD` overwrite)

Reason for read-and-reset: avoids double-counting between ticks and keeps DB writes as bounded deltas.

Failure behaviour: log and continue; next tick retries. No crash on transient Redis/DB errors.

## Error handling

- `[ApiController]` handles model-validation errors automatically → 400
- Controllers map "not found" by returning `NotFound()` (404)
- One lightweight global exception middleware (~15 lines) catches anything uncaught and returns `{ error: "internal_error" }` with 500
- Structured error depth is deferred to POC #4

## Frontend (`wwwroot/index.html`)

One static HTML file. No build step, no npm. Served by `app.UseStaticFiles()` at `/`.

- Form: input for a URL, "Shorten" button → POSTs to `/api/links`, displays the returned short URL as a clickable link
- List: "Top 10 most clicked" — calls `GET /api/links/top` every 5 s and renders a `<table>`

Total size target: <150 lines including minimal CSS.

## Local development

```bash
git clone <your-fork>
cd url-shortener-poc
docker-compose up -d                # starts mssql + redis
cd src/UrlShortener.Api
dotnet run                          # api listens on http://localhost:5000
# browse:
#   http://localhost:5000            demo frontend
#   http://localhost:5000/swagger    API docs
```

`docker-compose.yml` defines:

- `mssql`: `mcr.microsoft.com/mssql/server:2022-latest`, port 1433, sa password `Local-Dev-Password-1!` (clearly labelled local-only)
- `redis`: `redis:7-alpine`, port 6379, no password (local only)

Schema setup: a tiny idempotent startup routine in `Program.cs` runs `CREATE TABLE IF NOT EXISTS`-style SQL via SqlSugar on first boot. Formal migrations are POC #8.

## Repo safety for GitHub

- `.gitignore`: `bin/`, `obj/`, `appsettings.local.json`, `.env`, `*.user`, `.vs/`
- `appsettings.json`: only local defaults (`localhost`, dev password). Committed.
- No API keys, no tokens, no production connection strings anywhere in the repo.
- `docker-compose.yml` password is labelled "DEV ONLY — do not reuse in production".
- `README.md` explains the whole thing and warns against reusing the dev credentials.

## Success criteria

The POC is complete when:

1. `docker-compose up -d && dotnet run` produces a working API on `localhost:5000`
2. The frontend can shorten a URL and follow the redirect
3. The click counter ticks up on every visit (visible in the stats endpoint)
4. After ~60 s of clicks, the `ClickCount` column in MSSQL reflects the Redis total
5. The leaderboard endpoint returns the top 10 in descending order
6. Swagger UI at `/swagger` lists all four endpoints with working "Try it out"
7. Repo pushes to GitHub with no credentials exposed

## Teaching artefacts (expected in implementation)

During implementation, each file will include comments explaining:
`localhost:5000`
2. The frontend can shorten a URL and follow the redirect
3. The click counter ticks up on every visit (visible in the stats endpoint)
4. After ~60 s of clicks, the `ClickCount` column in MSSQL reflects the Redis total
5. The leaderboard endpoint returns the top 10 in descending order
6. Swagger UI at `/swagger` lists all four endpoints with working "Try it out"
7. Repo pushes to GitHub with no credentials exposed

## Teaching artefacts (expected in implementation)

During implementation, each file will include comments explaining:

- DI lifetimes (`AddSingleton` vs `AddScoped` vs `AddTransient`) and why each choice was made
- The cache-aside, counter, and sorted-set patterns at their call sites
- What the hosted service does and how it differs from a controller
- How SqlSugar maps C# types to SQL
- How FluentValidation plugs into the request pipeline

These comments make the POC self-documenting as a learning resource.
