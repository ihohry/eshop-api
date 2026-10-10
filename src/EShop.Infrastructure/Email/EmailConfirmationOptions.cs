using System.ComponentModel.DataAnnotations;

namespace EShop.Infrastructure.Email;

public sealed class EmailConfirmationOptions : IValidatableObject
{
    public const string SectionName = "Email:Confirmation";

    /// <summary>Link sent to the user, for example https://app.example.com/confirm-email?userId={userId}&amp;token={token}</summary>
    [Required]
    public string UrlTemplate { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!UrlTemplate.Contains("{userId}", StringComparison.Ordinal)
            || !UrlTemplate.Contains("{token}", StringComparison.Ordinal))
        {
            yield return new ValidationResult(
                "UrlTemplate must contain {userId} and {token}.", [nameof(UrlTemplate)]);
        }

        if (!Uri.TryCreate(UrlTemplate, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            yield return new ValidationResult(
                "UrlTemplate must be an absolute http(s) URL.", [nameof(UrlTemplate)]);
        }
    }
}