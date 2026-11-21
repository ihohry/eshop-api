using System.ComponentModel.DataAnnotations;

namespace EShop.Infrastructure.Email;

public sealed class EmailRateLimitOptions
{
    public const string SectionName = "Email:RateLimit";

    [Range(1, 10000)]
    public int PermitLimit { get; init; } = 3;

    [Range(1, 86400)]
    public int WindowSeconds { get; init; } = 3600;

    [Range(1000, 1000000)]
    public int MaxEntries { get; init; } = 100000;
}