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

    public async Task<ShortLink?> FindBySlugAsync(string slug)
        => await _db.Queryable<ShortLink>().FirstAsync(x => x.Slug == slug);

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
