using System.ComponentModel.DataAnnotations;

namespace EShop.Infrastructure.Payments;

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    [Required, RegularExpression("^sk_(test|live)_.+")]
    public string SecretKey { get; init; } = string.Empty;

    [Required, Url]
    public string SuccessUrlTemplate { get; init; } = string.Empty;

    [Required, Url]
    public string CancelUrlTemplate { get; init; } = string.Empty;
}