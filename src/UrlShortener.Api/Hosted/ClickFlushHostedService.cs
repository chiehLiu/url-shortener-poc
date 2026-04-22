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
