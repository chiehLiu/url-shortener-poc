namespace UrlShortener.Core.Dtos;

public sealed record LinkStatsResponse(
    string Slug,
    string TargetUrl,
    int ClickCount,
    DateTime CreatedAt);
