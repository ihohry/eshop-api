using System.ComponentModel.DataAnnotations;
using MailKit.Security;

namespace EShop.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    [Required] public string Host { get; init; } = string.Empty;
    [Range(1, 65535)] public int Port { get; init; } = 587;
    public SecureSocketOptions Security { get; init; } = SecureSocketOptions.StartTls;
    public string? Username { get; init; }
    public string? Password { get; init; }   // secret: user-secrets or env var
    [Required, EmailAddress] public string FromAddress { get; init; } = string.Empty;
    [Required] public string FromName { get; init; } = "EShop";
}