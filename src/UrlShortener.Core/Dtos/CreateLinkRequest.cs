namespace UrlShortener.Core.Dtos;

// DTO = Data Transfer Object. The shape we accept/return over HTTP.
// Keep DTOs separate from entities so the DB schema isn't leaked to API clients.
// 'record' is C# shorthand for an immutable value-like class — like a TS type.
public sealed record CreateLinkRequest(string TargetUrl);
