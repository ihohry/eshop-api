using System.ComponentModel.DataAnnotations;

namespace EShop.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Identity:Jwt";

    [Required]
    public string Issuer { get; set; } = "eshop-api";

    [Required]
    public string Audience { get; set; } = "eshop-api";

    // No default on purpose: the key is a secret and must come from user-secrets or an environment variable.
    // HS256 needs at least 256 bits, so 32 characters is the minimum.
    [Required, MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 7;
}