using System.ComponentModel.DataAnnotations;

namespace EShop.Infrastructure.Email;

public sealed class PasswordResetOptions : IValidatableObject
{
    public const string SectionName = "Email:PasswordReset";

    [Required]
    public string UrlTemplate { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!UrlTemplate.Contains("{userId}", StringComparison.Ordinal)
            || !UrlTemplate.Contains("{token}", StringComparison.Ordinal))
        {
            yield return new ValidationResult(
                "UrlTemplate must contain {userId} and {token}.",
                [nameof(UrlTemplate)]);
        }
    }
}