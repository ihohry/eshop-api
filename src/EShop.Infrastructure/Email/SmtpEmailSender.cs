using EShop.Domain.Email;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EShop.Infrastructure.Email;

public sealed partial class SmtpEmailSender(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var o = options.Value;

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(o.FromName, o.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain") { Text = message.Body };

        using var client = new SmtpClient();
        await client.ConnectAsync(o.Host, o.Port, o.Security, cancellationToken);
        if (!string.IsNullOrEmpty(o.Username))
        {
            await client.AuthenticateAsync(o.Username, o.Password ?? string.Empty, cancellationToken);
        }
        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        LogSent(logger, message.Subject);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email sent: {Subject}")]
    private static partial void LogSent(ILogger logger, string subject);
}