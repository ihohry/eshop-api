using System.ComponentModel.DataAnnotations;

namespace EShop.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Range(1, 300)]
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>Logs SQL parameter values. Development only.</summary>
    public bool EnableSensitiveDataLogging { get; set; }
}