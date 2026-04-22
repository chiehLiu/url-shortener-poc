using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using UrlShortener.Core.Dtos;
using UrlShortener.Core.Interfaces;

namespace UrlShortener.Api.Controllers;

// One controller class = one group of related routes. Same idea as a
// routes/links.js file in Express. Attributes tell ASP.NET which method
// handles which URL.
[ApiController]
public sealed class LinksController : ControllerBase
{
    private readonly ILinkService _service;
    private readonly IValidator<CreateLinkRequest> _validator;

    public LinksController(ILinkService service, IValidator<CreateLinkRequest> validator)
    {
        _service = service;
        _validator = validator;
    }

    [HttpPost("/api/links")]
    public async Task<ActionResult<LinkCreatedResponse>> Create([FromBody] CreateLinkRequest req)
    {
        // Explicit validation. 400 response on failure.
        var validation = await _validator.ValidateAsync(req);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var result = await _service.CreateAsync(req.TargetUrl, baseUrl);
        return Ok(result);
    }

    // :length(6) is a route constraint — only 6-character segments match.
    // Keeps this catch-all route from swallowing other paths like "swagger".
    [HttpGet("/{slug:length(6)}")]
    public async Task<IActionResult> FollowShortLink(string slug)
    {
        var target = await _service.ResolveAndClickAsync(slug);
        return target is null
            ? NotFound()
            : Redirect(target);
    }

    [HttpGet("/api/links/{slug}/stats")]
    public async Task<ActionResult<LinkStatsResponse>> GetStats(string slug)
    {
        var stats = await _service.GetStatsAsync(slug);
        return stats is null ? NotFound() : Ok(stats);
    }

    [HttpGet("/api/links/top")]
    public async Task<ActionResult<IReadOnlyList<TopLinkResponse>>> GetTop()
        => Ok(await _service.GetTopAsync());
}
