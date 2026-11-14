using System.Buffers.Text;
using System.Text;
using EShop.Infrastructure.Email;
using Microsoft.Extensions.Options;

namespace EShop.Infrastructure.Identity;

public sealed class AccountEmailLinks(
    IOptions<EmailConfirmationOptions> confirmationOptions,
    IOptions<PasswordResetOptions> resetOptions)
{
    public string Confirmation(Guid userId, string token) =>
        Build(confirmationOptions.Value.UrlTemplate, userId, token);

    public string PasswordReset(Guid userId, string token) =>
        Build(resetOptions.Value.UrlTemplate, userId, token);

    private static string Build(string template, Guid userId, string token) =>
        template
            .Replace("{userId}", userId.ToString(), StringComparison.Ordinal)
            .Replace("{token}", Base64Url.EncodeToString(Encoding.UTF8.GetBytes(token)), StringComparison.Ordinal);
}