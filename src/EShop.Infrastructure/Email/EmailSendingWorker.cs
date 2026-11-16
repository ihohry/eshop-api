using EShop.Domain.Email;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EShop.Infrastructure.Email;

public sealed partial class EmailSendingWorker(
    BackgroundEmailDispatcher dispatcher,
    EShop.Domain.Email.IEmailSender sender,
    ILogger<EmailSendingWorker> logger) : BackgroundService
{
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in dispatcher.Reader.ReadAllAsync(stoppingToken))
            {
                await SendAsync(message, CancellationToken.None);
            }
        }
        catch (OperationCanceledException ex)
        {
            LogStopping(logger, ex);
        }

        await DrainAsync();
    }

    private async Task DrainAsync()
    {
        using var timeout = new CancellationTokenSource(DrainTimeout);

        while (dispatcher.Reader.TryRead(out var message))
        {
            if (timeout.IsCancellationRequested)
            {
                LogDrainTimedOut(logger);
                return;
            }

            await SendAsync(message, timeout.Token);
        }
    }

    private async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await sender.SendAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            LogSendFailed(logger, ex, message.Subject);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Email worker is stopping; sending the messages that are still waiting")]
    private static partial void LogStopping(ILogger logger, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Email worker stopped before the queue was empty; the remaining messages are lost")]
    private static partial void LogDrainTimedOut(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Queued email could not be sent. Subject: {Subject}")]
    private static partial void LogSendFailed(ILogger logger, Exception exception, string subject);
}