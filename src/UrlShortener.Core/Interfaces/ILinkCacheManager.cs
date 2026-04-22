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
