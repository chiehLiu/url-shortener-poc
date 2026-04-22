using FluentValidation;
using UrlShortener.Core.Dtos;

namespace UrlShortener.Core.Validators;

// FluentValidation is the server-side equivalent of Zod / Yup.
// This gets called explicitly from the controller (LinksController.Create).
public sealed class CreateLinkRequestValidator : AbstractValidator<CreateLinkRequest>
{
    public CreateLinkRequestValidator()
    {
        RuleFor(x => x.TargetUrl)
            .NotEmpty()
            .MaximumLength(2048)
            .Must(BeAValidHttpUrl)
            .WithMessage("TargetUrl must be an absolute http or https URL.");
    }

    private static bool BeAValidHttpUrl(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var u)
           && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);
}
