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
