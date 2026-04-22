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
