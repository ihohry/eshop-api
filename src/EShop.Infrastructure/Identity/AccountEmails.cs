using EShop.Domain.Email;
using Microsoft.Extensions.Logging;

namespace EShop.Infrastructure.Identity;

public sealed partial class AccountEmails(
    IEmailDispatcher dispatcher,
    IEmailRateLimiter rateLimiter,
    AccountEmailLinks links,
    ILogger<AccountEmails> logger)
{
    public bool TryAcquire(string email) => rateLimiter.TryAcquire(email);

    public async Task SendConfirmationAsync(Guid userId, string to, string token, CancellationToken ct)
    {
        var link = links.Confirmation(userId, token);

        var body = $"Welcome to EShop!\n\nPlease confirm your email address:\n{link}\n\n" +
                "The link is valid for 24 hours. If you did not create an account, ignore this message.";

        await dispatcher.DispatchAsync(new EmailMessage(to, "Confirm your email", body), ct);
        LogConfirmationEmailQueued(logger, userId);
    }

    public async Task SendPasswordResetAsync(Guid userId, string to, string token, CancellationToken ct)
    {
        var link = links.PasswordReset(userId, token);

        var body = $"You asked to reset your EShop password:\n{link}\n\n" +
                "The link is valid for 24 hours. If you did not ask for this, ignore this message; your password stays the same.";

        await dispatcher.DispatchAsync(new EmailMessage(to, "Reset your password", body), ct);
        LogPasswordResetEmailQueued(logger, userId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Confirmation email queued: {UserId}")]
    private static partial void LogConfirmationEmailQueued(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset email queued: {UserId}")]
    private static partial void LogPasswordResetEmailQueued(ILogger logger, Guid userId);
}