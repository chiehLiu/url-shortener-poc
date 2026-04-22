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
