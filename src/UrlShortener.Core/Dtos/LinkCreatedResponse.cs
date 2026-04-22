namespace UrlShortener.Core.Dtos;

public sealed record LinkCreatedResponse(string Slug, string ShortUrl, string TargetUrl);
