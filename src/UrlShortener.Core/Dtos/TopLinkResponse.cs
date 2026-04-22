namespace UrlShortener.Core.Dtos;

public sealed record TopLinkResponse(string Slug, string TargetUrl, int ClickCount);
