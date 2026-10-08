using System.ComponentModel.DataAnnotations;

namespace EShop.Infrastructure.Identity;

public sealed class PasswordPolicyOptions
{
    public const string SectionName = "Identity:Password";

    [Range(8, 128)]
    public int RequiredLength { get; set; } = 10;

    [Range(1, 128)]
    public int RequiredUniqueChars { get; set; } = 4;

    public bool RequireDigit { get; set; } = true;

    public bool RequireLowercase { get; set; } = true;

    public bool RequireUppercase { get; set; } = true;

    public bool RequireNonAlphanumeric { get; set; }
}