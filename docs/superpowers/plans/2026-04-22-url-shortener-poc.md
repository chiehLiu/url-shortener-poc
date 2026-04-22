# URL Shortener POC Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a runnable end-to-end URL shortener using ASP.NET Core + SqlSugar + MSSQL + Redis that demonstrates cache-aside, atomic counters, sorted-set leaderboards, and background hosted services — as a hands-on introduction to .NET backend development for a frontend dev.

**Architecture:** Two-project .NET solution (`UrlShortener.Api` + `UrlShortener.Core`). `Controllers → Services → Repositories/CacheManager`. MSSQL holds persisted link records and a periodically-synced click count; Redis holds the hot-path cache, live counters, and the top-10 leaderboard. A `BackgroundService` flushes Redis counters into MSSQL every 30 seconds and rebuilds the leaderboard from DB truth.

**Tech Stack:** .NET 10, ASP.NET Core Web API, SqlSugar ORM, SQL Server 2022 (Linux container), StackExchange.Redis, FluentValidation, Swashbuckle, Docker Compose, vanilla HTML for the demo frontend.

---

## Notes before you start

### Testing approach for this plan

The spec (`2026-04-22-url-shortener-poc-design.md`) explicitly defers automated tests to POC #3 in the learning path. **This plan uses manual verification — `dotnet build`, `curl`, `docker logs`, and browser checks — in place of xUnit tests.** That is intentional so this POC stays focused on its three Redis patterns. If you're an agentic worker executing this plan, do **not** add unit tests.

### Teaching comments

Several files include teaching comments aimed at a developer who knows React/Vue/Express but is new to C# and .NET. Those comments are part of the design — keep them in the code.

### File structure overview

Every file that will exist at the end of this plan, with the task that creates it:

| File | Task |
|---|---|
| `UrlShortener.sln` | 1 |
| `src/UrlShortener.Api/UrlShortener.Api.csproj` | 1 |
| `src/UrlShortener.Api/Program.cs` (minimal stub) | 1 |
| `src/UrlShortener.Core/UrlShortener.Core.csproj` | 1 |
| NuGet package refs added | 2 |
| `.gitignore` | 3 |
| `docker-compose.yml` | 4 |
| `src/UrlShortener.Core/Entities/ShortLink.cs` | 5 |
| `src/UrlShortener.Core/Dtos/CreateLinkRequest.cs` | 6 |
| `src/UrlShortener.Core/Dtos/LinkCreatedResponse.cs` | 6 |
| `src/UrlShortener.Core/Dtos/LinkStatsResponse.cs` | 6 |
| `src/UrlShortener.Core/Dtos/TopLinkResponse.cs` | 6 |
| `src/UrlShortener.Core/Cache/RedisConnection.cs` | 7 |
| `src/UrlShortener.Core/Interfaces/ILinkCacheManager.cs` | 8 |
| `src/UrlShortener.Core/Cache/LinkCacheManager.cs` | 8 |
| `src/UrlShortener.Core/Interfaces/ILinkRepository.cs` | 9 |
| `src/UrlShortener.Core/Repositories/LinkRepository.cs` | 9 |
| `src/UrlShortener.Core/Utilities/SlugGenerator.cs` | 10 |
| `src/UrlShortener.Core/Validators/CreateLinkRequestValidator.cs` | 11 |
| `src/UrlShortener.Core/Interfaces/ILinkService.cs` | 12 |
| `src/UrlShortener.Core/Services/LinkService.cs` | 12 |
| `src/UrlShortener.Api/Controllers/LinksController.cs` | 13 |
| `src/UrlShortener.Api/appsettings.json` | 14 |
| `src/UrlShortener.Api/appsettings.Development.json` | 14 |
| `src/UrlShortener.Api/Hosted/ClickFlushHostedService.cs` | 15 |
| `src/UrlShortener.Api/Program.cs` (full composition root) | 16 |
| `src/UrlShortener.Api/wwwroot/index.html` | 18 |
| `src/UrlShortener.Api/Dockerfile` | 19 |
| `README.md` | 20 |

---

## Task 1: Initialize solution and projects

**Files:**
- Create: `UrlShortener.sln`
- Create: `src/UrlShortener.Api/UrlShortener.Api.csproj`
- Create: `src/UrlShortener.Api/Program.cs`
- Create: `src/UrlShortener.Core/UrlShortener.Core.csproj`

- [ ] **Step 1: Scaffold the Web API project**

Run from the repo root (`/Users/chieh.liu/SideProject/url-shortener-poc`):

```bash
dotnet new sln -n UrlShortener
dotnet new webapi -n UrlShortener.Api -o src/UrlShortener.Api --framework net10.0 --no-https false
dotnet new classlib -n UrlShortener.Core -o src/UrlShortener.Core --framework net10.0
```

- [ ] **Step 2: Add both projects to the solution**

```bash
dotnet sln add src/UrlShortener.Api/UrlShortener.Api.csproj
dotnet sln add src/UrlShortener.Core/UrlShortener.Core.csproj
```

- [ ] **Step 3: Make Api reference Core**

```bash
dotnet add src/UrlShortener.Api/UrlShortener.Api.csproj reference src/UrlShortener.Core/UrlShortener.Core.csproj
```

- [ ] **Step 4: Remove the template boilerplate**

The `webapi` template created `WeatherForecast.cs`, `Controllers/WeatherForecastController.cs`, and a chunky `Program.cs`. Delete them:

```bash
rm -f src/UrlShortener.Api/WeatherForecast.cs
rm -rf src/UrlShortener.Api/Controllers
rm -f src/UrlShortener.Core/Class1.cs
```

Replace `src/UrlShortener.Api/Program.cs` with a minimal stub (the full composition root comes in Task 16):

```csharp
// Program.cs is the entry point of an ASP.NET Core app.
// For a frontend dev: think of it as server.js in Express —
// it wires up middleware, services, and routes, then starts listening.

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/", () => "URL Shortener POC - wiring coming in Task 16");
app.Run();
```

- [ ] **Step 5: Verify the scaffolding builds**

Run:
```bash
dotnet build
```
Expected: `Build succeeded.` with 0 errors. You may see warnings — ignore for now.

- [ ] **Step 6: Commit**

```bash
git add .
git commit -m "chore: scaffold solution with Api + Core projects"
```

---

## Task 2: Add NuGet packages

**Files:**
- Modify: `src/UrlShortener.Core/UrlShortener.Core.csproj`
- Modify: `src/UrlShortener.Api/UrlShortener.Api.csproj`

- [ ] **Step 1: Add packages to `UrlShortener.Core`**

These are the domain-layer libraries: ORM, Redis client, and validation.

```bash
dotnet add src/UrlShortener.Core package SqlSugarCore --version 5.1.4.174
dotnet add src/UrlShortener.Core package StackExchange.Redis --version 2.8.16
dotnet add src/UrlShortener.Core package FluentValidation --version 12.1.1
dotnet add src/UrlShortener.Core package Microsoft.Extensions.Configuration.Abstractions --version 10.0.0
dotnet add src/UrlShortener.Core package Microsoft.Extensions.Logging.Abstractions --version 10.0.0
```

- [ ] **Step 2: Add packages to `UrlShortener.Api`**

Swagger only — the runtime, DI, and web framework come with `Microsoft.NET.Sdk.Web`.

```bash
dotnet add src/UrlShortener.Api package Swashbuckle.AspNetCore --version 10.1.0
```

- [ ] **Step 3: Verify restore + build**

```bash
dotnet build
```
Expected: `Build succeeded.`, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "chore: add SqlSugar, StackExchange.Redis, FluentValidation, Swashbuckle"
```

---

## Task 3: Create `.gitignore`

**Files:**
- Create: `.gitignore`

- [ ] **Step 1: Write `.gitignore`**

Path: `/Users/chieh.liu/SideProject/url-shortener-poc/.gitignore`

```gitignore
# Build output
bin/
obj/
*.user
.vs/

# Rider / VS Code
.idea/
.vscode/

# Local config overrides (never commit)
appsettings.local.json
appsettings.*.local.json
.env
.env.*

# OS
.DS_Store
Thumbs.db

# Docker volume data (if you ever mount one)
.docker/
```

- [ ] **Step 2: Commit**

```bash
git add .gitignore
git commit -m "chore: add .gitignore"
```

---

## Task 4: Docker Compose for MSSQL + Redis

**Files:**
- Create: `docker-compose.yml`

- [ ] **Step 1: Write `docker-compose.yml`**

Path: `/Users/chieh.liu/SideProject/url-shortener-poc/docker-compose.yml`

```yaml
# Local development only. The SA password below is a throwaway
# DEV-ONLY credential. Never reuse in production.
services:
  mssql:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: urlshortener-mssql
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "Local-Dev-Password-1!"
    ports:
      - "1433:1433"
    healthcheck:
      test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Local-Dev-Password-1!' -C -Q 'SELECT 1' || exit 1"]
      interval: 10s
      retries: 10
      start_period: 20s

  redis:
    image: redis:7-alpine
    container_name: urlshortener-redis
    ports:
      - "6379:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      retries: 10
```

- [ ] **Step 2: Start the services**

```bash
docker compose up -d
```
Expected: two containers start. `docker compose ps` shows both `urlshortener-mssql` and `urlshortener-redis` as healthy after ~20 seconds.

- [ ] **Step 3: Smoke-test Redis**

```bash
docker exec urlshortener-redis redis-cli SET hello world
docker exec urlshortener-redis redis-cli GET hello
```
Expected: second command prints `"world"`.

- [ ] **Step 4: Smoke-test MSSQL**

```bash
docker exec urlshortener-mssql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'Local-Dev-Password-1!' -C \
  -Q "SELECT @@VERSION"
```
Expected: prints a SQL Server 2022 version banner.

- [ ] **Step 5: Commit**

```bash
git add docker-compose.yml
git commit -m "feat: docker-compose for local mssql + redis"
```

---

## Task 5: Create the `ShortLink` entity

**Files:**
- Create: `src/UrlShortener.Core/Entities/ShortLink.cs`

- [ ] **Step 1: Write the entity**

Path: `src/UrlShortener.Core/Entities/ShortLink.cs`

```csharp
using SqlSugar;

namespace UrlShortener.Core.Entities;

// An "entity" is the C# class that maps 1:1 to a DB row.
// SqlSugar uses these attributes to figure out the SQL schema.
// Equivalent in your frontend world: a TypeScript type that mirrors a DB record.
[SugarTable("ShortLinks")]
public sealed class ShortLink
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(Length = 16, IsNullable = false)]
    public string Slug { get; set; } = default!;

    [SugarColumn(Length = 2048, IsNullable = false)]
    public string TargetUrl { get; set; } = default!;

    public DateTime CreatedAt { get; set; }

    public int ClickCount { get; set; }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build
```
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/UrlShortener.Core/Entities/
git commit -m "feat(core): ShortLink entity"
```

---

## Task 6: Create DTOs

**Files:**
- Create: `src/UrlShortener.Core/Dtos/CreateLinkRequest.cs`
- Create: `src/UrlShortener.Core/Dtos/LinkCreatedResponse.cs`
- Create: `src/UrlShortener.Core/Dtos/LinkStatsResponse.cs`
- Create: `src/UrlShortener.Core/Dtos/TopLinkResponse.cs`

- [ ] **Step 1: Write `CreateLinkRequest.cs`**

Path: `src/UrlShortener.Core/Dtos/CreateLinkRequest.cs`

```csharp
namespace UrlShortener.Core.Dtos;

// DTO = Data Transfer Object. The shape we accept/return over HTTP.
// Keep DTOs separate from entities so the DB schema isn't leaked to API clients.
// 'record' is C# shorthand for an immutable value-like class — like a TS type.
public sealed record CreateLinkRequest(string TargetUrl);
```

- [ ] **Step 2: Write `LinkCreatedResponse.cs`**

Path: `src/UrlShortener.Core/Dtos/LinkCreatedResponse.cs`

```csharp
namespace UrlShortener.Core.Dtos;

public sealed record LinkCreatedResponse(string Slug, string ShortUrl, string TargetUrl);
```

- [ ] **Step 3: Write `LinkStatsResponse.cs`**

Path: `src/UrlShortener.Core/Dtos/LinkStatsResponse.cs`

```csharp
namespace UrlShortener.Core.Dtos;

public sealed record LinkStatsResponse(
    string Slug,
    string TargetUrl,
    int ClickCount,
    DateTime CreatedAt);
```

- [ ] **Step 4: Write `TopLinkResponse.cs`**

Path: `src/UrlShortener.Core/Dtos/TopLinkResponse.cs`

```csharp
namespace UrlShortener.Core.Dtos;

public sealed record TopLinkResponse(string Slug, string TargetUrl, int ClickCount);
```

- [ ] **Step 5: Verify build and commit**

```bash
dotnet build
git add src/UrlShortener.Core/Dtos/
git commit -m "feat(core): DTOs for create/stats/top endpoints"
```

---

## Task 7: Create the Redis connection helper

**Files:**
- Create: `src/UrlShortener.Core/Cache/RedisConnection.cs`

- [ ] **Step 1: Write `RedisConnection.cs`**

Path: `src/UrlShortener.Core/Cache/RedisConnection.cs`

```csharp
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace UrlShortener.Core.Cache;

// Thin wrapper around the StackExchange.Redis connection.
// Ventem.Core.Redis wraps this same library — we're writing the wrapper
// by hand so you see what it does.
//
// IMPORTANT: registered as a SINGLETON in DI. The StackExchange.Redis
// ConnectionMultiplexer is thread-safe and expensive to create, so the
// whole app shares ONE instance. Same pattern in api-member.
public sealed class RedisConnection : IDisposable
{
    public IConnectionMultiplexer Multiplexer { get; }
    public IDatabase Db => Multiplexer.GetDatabase();

    public RedisConnection(IConfiguration config)
    {
        var cs = config.GetConnectionString("Redis")
                 ?? throw new InvalidOperationException("ConnectionStrings:Redis missing");
        Multiplexer = ConnectionMultiplexer.Connect(cs);
    }

    public IServer GetServer()
    {
        // For IServer-level commands (like KEYS pattern scan) we need to
        // pick a specific endpoint. With a single-node local Redis, the
        // first endpoint is the only one.
        var endpoint = Multiplexer.GetEndPoints().First();
        return Multiplexer.GetServer(endpoint);
    }

    public void Dispose() => Multiplexer.Dispose();
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build
```
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/UrlShortener.Core/Cache/RedisConnection.cs
git commit -m "feat(core): RedisConnection wrapper"
```

---

## Task 8: Create `ILinkCacheManager` and `LinkCacheManager`

**Files:**
- Create: `src/UrlShortener.Core/Interfaces/ILinkCacheManager.cs`
- Create: `src/UrlShortener.Core/Cache/LinkCacheManager.cs`

- [ ] **Step 1: Write the interface**

Path: `src/UrlShortener.Core/Interfaces/ILinkCacheManager.cs`

```csharp
namespace UrlShortener.Core.Interfaces;

// Encapsulates every Redis interaction in the app.
// Controllers/services only ever talk to Redis through this interface.
// This is the same separation api-member uses (AnnouncementCacheManager,
// HeroBannerCacheManager, etc. — one cache manager per resource).
public interface ILinkCacheManager
{
    // Cache-aside read: returns null on miss.
    Task<string?> GetTargetUrlAsync(string slug);

    // Populate the cache after a DB read (cache-aside write).
    Task SetTargetUrlAsync(string slug, string targetUrl);

    // Atomic counter: returns the post-increment value.
    Task<long> IncrementClickAsync(string slug);

    // Read the live pending-click counter without consuming it.
    Task<long> GetPendingClicksAsync(string slug);

    // Read-and-delete all pending click counters. Each entry is (slug, count).
    Task<IReadOnlyList<(string Slug, long Count)>> FlushPendingClicksAsync();

    // Top-N leaderboard read from the sorted set.
    Task<IReadOnlyList<(string Slug, long Count)>> GetTopAsync(int n);

    // Replace the leaderboard sorted set with a fresh snapshot.
    Task RebuildLeaderboardAsync(IEnumerable<(string Slug, long Count)> items);
}
```

- [ ] **Step 2: Write the implementation**

Path: `src/UrlShortener.Core/Cache/LinkCacheManager.cs`

```csharp
using StackExchange.Redis;
using UrlShortener.Core.Interfaces;

namespace UrlShortener.Core.Cache;

// Concrete Redis implementation of ILinkCacheManager.
// Registered as SINGLETON — no per-request state, all data lives in Redis.
public sealed class LinkCacheManager : ILinkCacheManager
{
    private const string LinkPrefix = "link:";       // link:{slug} -> TargetUrl (TTL 1h)
    private const string ClickPrefix = "click:";     // click:{slug} -> integer counter (no TTL)
    private const string TopKey = "top-links";       // sorted set, score = click count
    private static readonly TimeSpan LinkTtl = TimeSpan.FromHours(1);

    private readonly RedisConnection _conn;

    public LinkCacheManager(RedisConnection conn) => _conn = conn;

    public async Task<string?> GetTargetUrlAsync(string slug)
    {
        var v = await _conn.Db.StringGetAsync(LinkPrefix + slug);
        return v.HasValue ? v.ToString() : null;
    }

    public Task SetTargetUrlAsync(string slug, string targetUrl)
        => _conn.Db.StringSetAsync(LinkPrefix + slug, targetUrl, LinkTtl);

    public Task<long> IncrementClickAsync(string slug)
        => _conn.Db.StringIncrementAsync(ClickPrefix + slug);

    public async Task<long> GetPendingClicksAsync(string slug)
    {
        var v = await _conn.Db.StringGetAsync(ClickPrefix + slug);
        return v.HasValue && long.TryParse(v, out var n) ? n : 0;
    }

    public async Task<IReadOnlyList<(string Slug, long Count)>> FlushPendingClicksAsync()
    {
        var result = new List<(string, long)>();
        var server = _conn.GetServer();

        // SCAN-based iteration over click:* keys. GETDEL atomically reads and
        // removes each counter so we don't double-count on the next tick.
        await foreach (var key in server.KeysAsync(pattern: ClickPrefix + "*"))
        {
            var val = await _conn.Db.StringGetDeleteAsync(key);
            if (val.HasValue && long.TryParse(val, out var count) && count > 0)
            {
                var slug = key.ToString()[ClickPrefix.Length..];
                result.Add((slug, count));
            }
        }
        return result;
    }

    public async Task<IReadOnlyList<(string Slug, long Count)>> GetTopAsync(int n)
    {
        var entries = await _conn.Db.SortedSetRangeByRankWithScoresAsync(
            TopKey, 0, n - 1, Order.Descending);
        return entries
            .Select(e => (e.Element.ToString(), (long)e.Score))
            .ToList();
    }

    public async Task RebuildLeaderboardAsync(IEnumerable<(string Slug, long Count)> items)
    {
        var entries = items.Select(i => new SortedSetEntry(i.Slug, i.Count)).ToArray();
        var batch = _conn.Db.CreateTransaction();
        _ = batch.KeyDeleteAsync(TopKey);
        if (entries.Length > 0)
            _ = batch.SortedSetAddAsync(TopKey, entries);
        await batch.ExecuteAsync();
    }
}
```

- [ ] **Step 3: Verify build**

```bash
dotnet build
```
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/UrlShortener.Core/Interfaces/ILinkCacheManager.cs src/UrlShortener.Core/Cache/LinkCacheManager.cs
git commit -m "feat(core): LinkCacheManager with cache-aside, counter, sorted set"
```

---

## Task 9: Create `ILinkRepository` and `LinkRepository`

**Files:**
- Create: `src/UrlShortener.Core/Interfaces/ILinkRepository.cs`
- Create: `src/UrlShortener.Core/Repositories/LinkRepository.cs`

- [ ] **Step 1: Write the interface**

Path: `src/UrlShortener.Core/Interfaces/ILinkRepository.cs`

```csharp
using UrlShortener.Core.Entities;

namespace UrlShortener.Core.Interfaces;

// Every DB interaction goes through this interface.
// The rest of the app never touches ISqlSugarClient directly — same rule as api-member.
public interface ILinkRepository
{
    Task<ShortLink?> FindBySlugAsync(string slug);
    Task<int> InsertAsync(ShortLink link);
    Task IncrementClickCountAsync(string slug, long delta);
    Task<List<ShortLink>> GetTopByClickCountAsync(int n);
}
```

- [ ] **Step 2: Write the implementation**

Path: `src/UrlShortener.Core/Repositories/LinkRepository.cs`

```csharp
using SqlSugar;
using UrlShortener.Core.Entities;
using UrlShortener.Core.Interfaces;

namespace UrlShortener.Core.Repositories;

// SqlSugar is the ORM — like a lighter EF Core / Prisma / TypeORM.
// ISqlSugarClient is the session; we get one per request (Scoped lifetime in DI).
public sealed class LinkRepository : ILinkRepository
{
    private readonly ISqlSugarClient _db;

    public LinkRepository(ISqlSugarClient db) => _db = db;

    public Task<ShortLink?> FindBySlugAsync(string slug)
        => _db.Queryable<ShortLink>().FirstAsync(x => x.Slug == slug);

    public async Task<int> InsertAsync(ShortLink link)
    {
        link.CreatedAt = DateTime.UtcNow;
        // ExecuteReturnIdentityAsync runs the INSERT and returns the generated Id.
        return await _db.Insertable(link).ExecuteReturnIdentityAsync();
    }

    public Task IncrementClickCountAsync(string slug, long delta)
        => _db.Updateable<ShortLink>()
              .SetColumns(x => new ShortLink { ClickCount = x.ClickCount + (int)delta })
              .Where(x => x.Slug == slug)
              .ExecuteCommandAsync();

    public Task<List<ShortLink>> GetTopByClickCountAsync(int n)
        => _db.Queryable<ShortLink>()
              .OrderBy(x => x.ClickCount, OrderByType.Desc)
              .Take(n)
              .ToListAsync();
}
```

- [ ] **Step 3: Verify build**

```bash
dotnet build
```
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/UrlShortener.Core/Interfaces/ILinkRepository.cs src/UrlShortener.Core/Repositories/
git commit -m "feat(core): LinkRepository with SqlSugar"
```

---

## Task 10: Slug generator utility

**Files:**
- Create: `src/UrlShortener.Core/Utilities/SlugGenerator.cs`

- [ ] **Step 1: Write the utility**

Path: `src/UrlShortener.Core/Utilities/SlugGenerator.cs`

```csharp
using System.Security.Cryptography;

namespace UrlShortener.Core.Utilities;

// Generates a short, URL-safe random identifier.
// 6 base62 chars = 62^6 ≈ 56 billion combinations. Collisions are rare;
// the service retries once on a unique-constraint violation.
public static class SlugGenerator
{
    private const string Alphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public static string Generate(int length = 6)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        return new string(chars);
    }
}
```

- [ ] **Step 2: Verify build and commit**

```bash
dotnet build
git add src/UrlShortener.Core/Utilities/
git commit -m "feat(core): SlugGenerator utility"
```

---

## Task 11: `CreateLinkRequestValidator`

**Files:**
- Create: `src/UrlShortener.Core/Validators/CreateLinkRequestValidator.cs`

- [ ] **Step 1: Write the validator**

Path: `src/UrlShortener.Core/Validators/CreateLinkRequestValidator.cs`

```csharp
using FluentValidation;
using UrlShortener.Core.Dtos;

namespace UrlShortener.Core.Validators;

// FluentValidation is the server-side equivalent of Zod / Yup.
// This gets called explicitly from the controller in Task 13.
public sealed class CreateLinkRequestValidator : AbstractValidator<CreateLinkRequest>
{
    public CreateLinkRequestValidator()
    {
        RuleFor(x => x.TargetUrl)
            .NotEmpty()
            .MaximumLength(2048)
            .Must(BeAValidHttpUrl)
            .WithMessage("TargetUrl must be an absolute http or https URL.");
    }

    private static bool BeAValidHttpUrl(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var u)
           && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);
}
```

- [ ] **Step 2: Verify build and commit**

```bash
dotnet build
git add src/UrlShortener.Core/Validators/
git commit -m "feat(core): CreateLinkRequestValidator"
```

---

## Task 12: `ILinkService` and `LinkService`

**Files:**
- Create: `src/UrlShortener.Core/Interfaces/ILinkService.cs`
- Create: `src/UrlShortener.Core/Services/LinkService.cs`

- [ ] **Step 1: Write the service interface**

Path: `src/UrlShortener.Core/Interfaces/ILinkService.cs`

```csharp
using UrlShortener.Core.Dtos;

namespace UrlShortener.Core.Interfaces;

// The "service" layer orchestrates repo + cache. Controllers call this;
// it hides the cache-aside and DB details from HTTP-land.
// Same pattern as api-member's MemberService, BetService, etc.
public interface ILinkService
{
    Task<LinkCreatedResponse> CreateAsync(string targetUrl, string baseUrl);
    Task<string?> ResolveAndClickAsync(string slug);
    Task<LinkStatsResponse?> GetStatsAsync(string slug);
    Task<IReadOnlyList<TopLinkResponse>> GetTopAsync();
}
```

- [ ] **Step 2: Write the service implementation**

Path: `src/UrlShortener.Core/Services/LinkService.cs`

```csharp
using UrlShortener.Core.Dtos;
using UrlShortener.Core.Entities;
using UrlShortener.Core.Interfaces;
using UrlShortener.Core.Utilities;

namespace UrlShortener.Core.Services;

public sealed class LinkService : ILinkService
{
    private readonly ILinkRepository _repo;
    private readonly ILinkCacheManager _cache;

    public LinkService(ILinkRepository repo, ILinkCacheManager cache)
    {
        _repo = repo;
        _cache = cache;
    }

    public async Task<LinkCreatedResponse> CreateAsync(string targetUrl, string baseUrl)
    {
        // Try a fresh slug; retry once on a DB unique-constraint collision.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var slug = SlugGenerator.Generate();
            try
            {
                await _repo.InsertAsync(new ShortLink { Slug = slug, TargetUrl = targetUrl });
                return new LinkCreatedResponse(
                    slug,
                    $"{baseUrl.TrimEnd('/')}/{slug}",
                    targetUrl);
            }
            catch when (attempt == 0)
            {
                // swallow and retry once
            }
        }
        throw new InvalidOperationException("Could not generate a unique slug after 2 attempts.");
    }

    // The hot path. Demonstrates cache-aside + atomic counter.
    public async Task<string?> ResolveAndClickAsync(string slug)
    {
        // 1. Try cache.
        var url = await _cache.GetTargetUrlAsync(slug);

        // 2. On miss, read DB and backfill cache.
        if (url is null)
        {
            var link = await _repo.FindBySlugAsync(slug);
            if (link is null) return null;
            url = link.TargetUrl;
            await _cache.SetTargetUrlAsync(slug, url);
        }

        // 3. Bump the live click counter. Fire-and-forget semantically,
        //    but we await it so exceptions don't get swallowed.
        await _cache.IncrementClickAsync(slug);
        return url;
    }

    public async Task<LinkStatsResponse?> GetStatsAsync(string slug)
    {
        var link = await _repo.FindBySlugAsync(slug);
        if (link is null) return null;

        // Live total = persisted count + pending count that hasn't been flushed yet.
        var pending = await _cache.GetPendingClicksAsync(slug);
        return new LinkStatsResponse(
            slug,
            link.TargetUrl,
            link.ClickCount + (int)pending,
            link.CreatedAt);
    }

    public async Task<IReadOnlyList<TopLinkResponse>> GetTopAsync()
    {
        var top = await _cache.GetTopAsync(10);
        var result = new List<TopLinkResponse>(top.Count);

        foreach (var (slug, count) in top)
        {
            var link = await _repo.FindBySlugAsync(slug);
            if (link is not null)
                result.Add(new TopLinkResponse(slug, link.TargetUrl, (int)count));
        }
        return result;
    }
}
```

- [ ] **Step 3: Verify build and commit**

```bash
dotnet build
git add src/UrlShortener.Core/Interfaces/ILinkService.cs src/UrlShortener.Core/Services/
git commit -m "feat(core): LinkService orchestrating repo + cache"
```

---

## Task 13: `LinksController`

**Files:**
- Create: `src/UrlShortener.Api/Controllers/LinksController.cs`

- [ ] **Step 1: Write the controller**

Path: `src/UrlShortener.Api/Controllers/LinksController.cs`

```csharp
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using UrlShortener.Core.Dtos;
using UrlShortener.Core.Interfaces;

namespace UrlShortener.Api.Controllers;

// One controller class = one group of related routes. Same idea as a
// routes/links.js file in Express. Attributes tell ASP.NET which method
// handles which URL.
[ApiController]
public sealed class LinksController : ControllerBase
{
    private readonly ILinkService _service;
    private readonly IValidator<CreateLinkRequest> _validator;

    public LinksController(ILinkService service, IValidator<CreateLinkRequest> validator)
    {
        _service = service;
        _validator = validator;
    }

    [HttpPost("/api/links")]
    public async Task<ActionResult<LinkCreatedResponse>> Create([FromBody] CreateLinkRequest req)
    {
        // Explicit validation. 400 response on failure.
        var validation = await _validator.ValidateAsync(req);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var result = await _service.CreateAsync(req.TargetUrl, baseUrl);
        return Ok(result);
    }

    // :length(6) is a route constraint — only 6-character segments match.
    // Keeps this catch-all route from swallowing other paths like "swagger".
    [HttpGet("/{slug:length(6)}")]
    public async Task<IActionResult> FollowShortLink(string slug)
    {
        var target = await _service.ResolveAndClickAsync(slug);
        return target is null
            ? NotFound()
            : Redirect(target);
    }

    [HttpGet("/api/links/{slug}/stats")]
    public async Task<ActionResult<LinkStatsResponse>> GetStats(string slug)
    {
        var stats = await _service.GetStatsAsync(slug);
        return stats is null ? NotFound() : Ok(stats);
    }

    [HttpGet("/api/links/top")]
    public async Task<ActionResult<IReadOnlyList<TopLinkResponse>>> GetTop()
        => Ok(await _service.GetTopAsync());
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build
```
Expected: 0 errors. (The app still won't run end-to-end until Task 16.)

- [ ] **Step 3: Commit**

```bash
git add src/UrlShortener.Api/Controllers/
git commit -m "feat(api): LinksController with 4 endpoints"
```

---

## Task 14: `appsettings.json`

**Files:**
- Create: `src/UrlShortener.Api/appsettings.json`
- Create: `src/UrlShortener.Api/appsettings.Development.json`

- [ ] **Step 1: Write `appsettings.json`**

Path: `src/UrlShortener.Api/appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "Mssql": "Server=localhost,1433;Database=urlshortener;User Id=sa;Password=Local-Dev-Password-1!;TrustServerCertificate=True;Encrypt=False",
    "Redis": "localhost:6379,allowAdmin=true"
  },
  "AllowedHosts": "*"
}
```

Note: `allowAdmin=true` is what lets `IServer.Keys(pattern:...)` work. In production you'd avoid this and track the pending-click keys another way; that's POC #8 territory.

- [ ] **Step 2: Write `appsettings.Development.json`**

Path: `src/UrlShortener.Api/appsettings.Development.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Information",
      "UrlShortener": "Debug"
    }
  }
}
```

- [ ] **Step 3: Commit**

```bash
git add src/UrlShortener.Api/appsettings.json src/UrlShortener.Api/appsettings.Development.json
git commit -m "chore(api): appsettings with local dev connection strings"
```

---

## Task 15: `ClickFlushHostedService`

**Files:**
- Create: `src/UrlShortener.Api/Hosted/ClickFlushHostedService.cs`

- [ ] **Step 1: Write the hosted service**

Path: `src/UrlShortener.Api/Hosted/ClickFlushHostedService.cs`

```csharp
using UrlShortener.Core.Interfaces;

namespace UrlShortener.Api.Hosted;

// A BackgroundService runs for the lifetime of the app, independent of HTTP.
// Same role as api-member's AppCacheManageHostedService.
// Every 30 seconds:
//   1. Flush pending click counters from Redis into MSSQL
//   2. Rebuild the top-links leaderboard from the DB's current top 10
public sealed class ClickFlushHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILinkCacheManager _cache;
    private readonly ILogger<ClickFlushHostedService> _log;

    public ClickFlushHostedService(
        IServiceScopeFactory scopeFactory,
        ILinkCacheManager cache,
        ILogger<ClickFlushHostedService> log)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("ClickFlushHostedService started; interval = {Interval}", Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "ClickFlushHostedService tick failed");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var pending = await _cache.FlushPendingClicksAsync();

        if (pending.Count > 0)
        {
            // ILinkRepository is Scoped — it needs its own scope per tick.
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ILinkRepository>();

            foreach (var (slug, delta) in pending)
            {
                ct.ThrowIfCancellationRequested();
                await repo.IncrementClickCountAsync(slug, delta);
            }

            _log.LogInformation("Flushed {Count} click counters to DB", pending.Count);
        }

        // Always rebuild the leaderboard, even if no new clicks came in —
        // cheap query, keeps things simple.
        using (var scope = _scopeFactory.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ILinkRepository>();
            var top = await repo.GetTopByClickCountAsync(10);
            await _cache.RebuildLeaderboardAsync(
                top.Select(l => (l.Slug, (long)l.ClickCount)));
        }
    }
}
```

- [ ] **Step 2: Verify build and commit**

```bash
dotnet build
git add src/UrlShortener.Api/Hosted/
git commit -m "feat(api): ClickFlushHostedService flushes counters every 30s"
```

---

## Task 16: Wire up `Program.cs`

**Files:**
- Modify: `src/UrlShortener.Api/Program.cs`

- [ ] **Step 1: Replace the stub `Program.cs` with the full composition root**

Path: `src/UrlShortener.Api/Program.cs`

```csharp
using FluentValidation;
using SqlSugar;
using UrlShortener.Api.Hosted;
using UrlShortener.Core.Cache;
using UrlShortener.Core.Entities;
using UrlShortener.Core.Interfaces;
using UrlShortener.Core.Repositories;
using UrlShortener.Core.Services;
using UrlShortener.Core.Validators;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Data access: SqlSugar
// ---------------------------------------------------------------------------
// Scoped = one instance per HTTP request (and one per hosted-service scope).
// Same choice api-member makes — ISqlSugarClient is not thread-safe for
// concurrent queries on the same connection.
builder.Services.AddScoped<ISqlSugarClient>(_ =>
{
    var cs = builder.Configuration.GetConnectionString("Mssql")
             ?? throw new InvalidOperationException("Missing ConnectionStrings:Mssql");
    return new SqlSugarClient(new ConnectionConfig
    {
        ConnectionString = cs,
        DbType = DbType.SqlServer,
        IsAutoCloseConnection = true,
        InitKeyType = InitKeyType.Attribute,
    });
});

// ---------------------------------------------------------------------------
// Redis: single shared connection for the whole app
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<RedisConnection>();
builder.Services.AddSingleton<ILinkCacheManager, LinkCacheManager>();

// ---------------------------------------------------------------------------
// Domain services
// ---------------------------------------------------------------------------
builder.Services.AddScoped<ILinkRepository, LinkRepository>();
builder.Services.AddScoped<ILinkService, LinkService>();

// ---------------------------------------------------------------------------
// Validation
// ---------------------------------------------------------------------------
builder.Services.AddValidatorsFromAssemblyContaining<CreateLinkRequestValidator>();

// ---------------------------------------------------------------------------
// Web
// ---------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// ---------------------------------------------------------------------------
// Background services
// ---------------------------------------------------------------------------
builder.Services.AddHostedService<ClickFlushHostedService>();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Schema bootstrap (POC-style; proper migrations are POC #8)
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
    db.DbMaintenance.CreateDatabase();           // no-op if the DB already exists
    db.CodeFirst.InitTables<ShortLink>();        // creates the ShortLinks table on first run
}

// ---------------------------------------------------------------------------
// Global exception middleware — tiny, POC-only.
// Proper error handling is POC #4.
// ---------------------------------------------------------------------------
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Unhandled exception");
        ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await ctx.Response.WriteAsJsonAsync(new { error = "internal_error" });
    }
});

app.UseDefaultFiles();  // maps / -> /index.html
app.UseStaticFiles();   // serves files from wwwroot/
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.MapControllers();

app.Run();
```

- [ ] **Step 2: Verify build**

```bash
dotnet build
```
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/UrlShortener.Api/Program.cs
git commit -m "feat(api): compose all services in Program.cs"
```

---

## Task 17: First end-to-end smoke test

This task has no file changes — only verification.

- [ ] **Step 1: Make sure Docker services are running**

```bash
docker compose ps
```
Expected: both `urlshortener-mssql` and `urlshortener-redis` show `healthy`.

- [ ] **Step 2: Run the API**

In one terminal:
```bash
cd src/UrlShortener.Api
dotnet run
```
Expected: logs include `Now listening on: http://localhost:5000` (or similar) and `ClickFlushHostedService started`.

If port differs, note the actual URL from the log line — subsequent curls use that.

- [ ] **Step 3: Confirm Swagger loads**

Open `http://localhost:5000/swagger` in a browser. You should see the three API endpoints listed (`POST /api/links`, `GET /api/links/{slug}/stats`, `GET /api/links/top`; the redirect route isn't shown because it doesn't carry an `[Api...]` doc hint — that's OK).

- [ ] **Step 4: Create a link via curl**

In a second terminal:
```bash
curl -i -X POST http://localhost:5000/api/links \
  -H 'Content-Type: application/json' \
  -d '{"targetUrl": "https://example.com"}'
```
Expected: HTTP 200 with a JSON body like:
```json
{ "slug":"Ab3xZ9", "shortUrl":"http://localhost:5000/Ab3xZ9", "targetUrl":"https://example.com" }
```

- [ ] **Step 5: Follow the short link**

```bash
curl -i http://localhost:5000/<slug-from-above>
```
Expected: HTTP 302 with `Location: https://example.com`.

- [ ] **Step 6: Check stats — click count should be 1**

```bash
curl http://localhost:5000/api/links/<slug>/stats
```
Expected: `"clickCount": 1`. (This is the live count: DB 0 + Redis pending 1.)

- [ ] **Step 7: Validation rejects bad input**

```bash
curl -i -X POST http://localhost:5000/api/links \
  -H 'Content-Type: application/json' \
  -d '{"targetUrl": "not-a-url"}'
```
Expected: HTTP 400 with a validation error about `TargetUrl`.

- [ ] **Step 8: Wait ~30 s, then verify the hosted service flushed counters**

Watch the API's console logs for `Flushed N click counters to DB`. After that, re-run the stats call:
```bash
curl http://localhost:5000/api/links/<slug>/stats
```
Expected: `clickCount` still 1, but now the DB row has been updated. You can confirm:
```bash
docker exec urlshortener-mssql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'Local-Dev-Password-1!' -C \
  -d urlshortener -Q "SELECT Slug, ClickCount FROM ShortLinks"
```

- [ ] **Step 9: Leaderboard**

```bash
curl http://localhost:5000/api/links/top
```
Expected: a JSON array with your link at position 0 after the first flush tick runs.

- [ ] **Step 10: Stop the app**

`Ctrl+C` in the dotnet terminal.

- [ ] **Step 11: Commit (no code changes, but mark the milestone)**

```bash
git commit --allow-empty -m "chore: first end-to-end smoke test passed"
```

---

## Task 18: Demo frontend (`index.html`)

**Files:**
- Create: `src/UrlShortener.Api/wwwroot/index.html`

- [ ] **Step 1: Write the HTML**

Path: `src/UrlShortener.Api/wwwroot/index.html`

```html
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <title>URL Shortener POC</title>
  <style>
    body { font-family: system-ui, sans-serif; max-width: 640px; margin: 2rem auto; padding: 0 1rem; color: #222; }
    h1 { margin-bottom: 0.2rem; }
    p.sub { color: #666; margin-top: 0; }
    form { display: flex; gap: 0.5rem; margin: 1rem 0; }
    input { flex: 1; padding: 0.5rem; font-size: 1rem; border: 1px solid #ccc; border-radius: 4px; }
    button { padding: 0.5rem 1rem; font-size: 1rem; border: 0; background: #1f6feb; color: white; border-radius: 4px; cursor: pointer; }
    button:hover { background: #1858c2; }
    #result { padding: 0.75rem; background: #f2f8ff; border-radius: 4px; margin-top: 0.5rem; word-break: break-all; min-height: 1.2em; }
    table { width: 100%; border-collapse: collapse; margin-top: 1rem; }
    th, td { padding: 0.4rem 0.5rem; text-align: left; border-bottom: 1px solid #eee; font-size: 0.9rem; }
    th { background: #fafafa; }
    td.count { text-align: right; font-variant-numeric: tabular-nums; }
    a { color: #1f6feb; }
  </style>
</head>
<body>
  <h1>URL Shortener POC</h1>
  <p class="sub">Paste a URL, get a short link. Open it in a new tab and watch the counter tick up.</p>

  <form id="f">
    <input id="url" type="url" placeholder="https://example.com" required />
    <button type="submit">Shorten</button>
  </form>
  <div id="result">—</div>

  <h2>Top 10 most clicked</h2>
  <table>
    <thead><tr><th>Slug</th><th>Target</th><th>Clicks</th></tr></thead>
    <tbody id="top"></tbody>
  </table>

  <script>
    const $f = document.getElementById('f');
    const $url = document.getElementById('url');
    const $result = document.getElementById('result');
    const $top = document.getElementById('top');

    $f.addEventListener('submit', async (e) => {
      e.preventDefault();
      $result.textContent = 'Shortening...';
      const res = await fetch('/api/links', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ targetUrl: $url.value })
      });
      if (!res.ok) { $result.textContent = 'Error: ' + res.status; return; }
      const body = await res.json();
      $result.innerHTML = `<a href="${body.shortUrl}" target="_blank">${body.shortUrl}</a> → ${body.targetUrl}`;
      $url.value = '';
    });

    async function refreshTop() {
      try {
        const res = await fetch('/api/links/top');
        const items = await res.json();
        $top.innerHTML = items.map(i =>
          `<tr><td>${i.slug}</td><td><a href="/${i.slug}" target="_blank">${i.targetUrl}</a></td><td class="count">${i.clickCount}</td></tr>`
        ).join('') || `<tr><td colspan="3" style="color:#888">No data yet — shorten a link and click it a few times.</td></tr>`;
      } catch { /* ignore transient errors */ }
    }

    refreshTop();
    setInterval(refreshTop, 5000);
  </script>
</body>
</html>
```

- [ ] **Step 2: Run the app and open the page**

```bash
cd src/UrlShortener.Api
dotnet run
```
Open `http://localhost:5000`. You should see the form and an empty table.

Paste a URL, click Shorten, click the resulting short link (opens in a new tab). Within 5 s the table updates; within 30 s the count reflects the DB-persisted value.

- [ ] **Step 3: Stop the app and commit**

```bash
git add src/UrlShortener.Api/wwwroot/
git commit -m "feat(api): demo frontend served from wwwroot"
```

---

## Task 19: `Dockerfile` for the API

**Files:**
- Create: `src/UrlShortener.Api/Dockerfile`

- [ ] **Step 1: Write the Dockerfile**

Path: `src/UrlShortener.Api/Dockerfile`

```dockerfile
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
```

- [ ] **Step 2: Build the image**

Run from the repo root:
```bash
docker build -t urlshortener-api -f src/UrlShortener.Api/Dockerfile .
```
Expected: builds successfully, ending with a tagged image.

- [ ] **Step 3: Commit**

```bash
git add src/UrlShortener.Api/Dockerfile
git commit -m "chore(api): multi-stage Dockerfile"
```

---

## Task 20: `README.md`

**Files:**
- Create: `README.md`

- [ ] **Step 1: Write the README**

Path: `/Users/chieh.liu/SideProject/url-shortener-poc/README.md`

```markdown
# URL Shortener POC

A tiny end-to-end URL shortener built as a hands-on introduction to .NET backend development.

This POC demonstrates, with a single app, the backbone patterns used in a
real production .NET API: layered architecture, Redis cache-aside, atomic
counters, sorted-set leaderboards, and background hosted services.

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

Prerequisites:
- .NET 10 SDK
- Docker Desktop

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
├── UrlShortener.sln
├── docs/superpowers/        design spec, implementation plan, learning path
└── src/
    ├── UrlShortener.Api/    web host, controllers, hosted services
    └── UrlShortener.Core/   entities, DTOs, services, repos, cache, validators
```

## Security notes

This is a **learning POC**. The MSSQL `sa` password in `docker-compose.yml` is a local-only throwaway value. Never reuse it. There are no secrets, API keys, or production connection strings in this repository.

## Where this fits in the bigger picture

This is Foundation POC #1 of an 8-POC backend learning path (see `docs/superpowers/specs/backend-learning-path.md`). The next POCs layer on auth, tests, validation depth, service-to-service HTTP, messaging, and observability.
```

- [ ] **Step 2: Commit**

```bash
git add README.md
git commit -m "docs: README with run instructions and scope"
```

---

## Task 21: Final end-to-end verification

This task has no file changes — only verification. It catches regressions from the frontend, Dockerfile, and README tasks.

- [ ] **Step 1: Full clean restart**

```bash
docker compose down
docker compose up -d
```
Wait ~20 s until both containers are healthy.

- [ ] **Step 2: Start the API**

```bash
cd src/UrlShortener.Api
dotnet run
```

- [ ] **Step 3: Use the frontend**

Open `http://localhost:5000`. Shorten a URL, click the short link a few times (open in new tabs), watch the "Top 10" table update within 5 s.

- [ ] **Step 4: Wait for a flush cycle**

After ~30 s, the API console logs should show `Flushed N click counters to DB`. Verify with:
```bash
docker exec urlshortener-mssql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'Local-Dev-Password-1!' -C \
  -d urlshortener -Q "SELECT TOP 10 Slug, ClickCount, CreatedAt FROM ShortLinks ORDER BY ClickCount DESC"
```
Expected: your link appears with the click count.

- [ ] **Step 5: Verify Swagger still lists endpoints**

`http://localhost:5000/swagger` → all four endpoints present, "Try it out" works.

- [ ] **Step 6: Stop the app**

`Ctrl+C` in the dotnet terminal. `docker compose down` optional.

- [ ] **Step 7: Final commit (milestone marker)**

```bash
git commit --allow-empty -m "chore: POC 1 complete — ready to push to GitHub"
```

- [ ] **Step 8: Create a GitHub repo and push**

(Do this manually when you're ready. The `git remote add origin <url>` and `git push -u origin main` step is the user's decision — the spec is explicit that no credentials are exposed, so the repo is safe to make public.)

---

## Self-review (done by the author before handoff)

- **Spec coverage:** Every scope item from the design spec maps to at least one task above. Cache-aside (Tasks 8, 12), `INCR` counter (Tasks 8, 12), sorted-set leaderboard (Tasks 8, 12, 15), background hosted service (Task 15), minimal HTML frontend (Task 18). ✅
- **Placeholder scan:** no `TBD`/`TODO`/`implement later` in any step. Every step has either a code block or an exact command. ✅
- **Type consistency:** method names (`GetTargetUrlAsync`, `IncrementClickAsync`, `FlushPendingClicksAsync`, `GetTopAsync`, `RebuildLeaderboardAsync`, `GetPendingClicksAsync`, `FindBySlugAsync`, `InsertAsync`, `IncrementClickCountAsync`, `GetTopByClickCountAsync`, `CreateAsync`, `ResolveAndClickAsync`, `GetStatsAsync`) are used identically in both their interface definitions and call sites. DTO record shapes match between the core library and the controller. ✅
