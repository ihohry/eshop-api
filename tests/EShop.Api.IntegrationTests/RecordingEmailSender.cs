using System.Threading.Channels;
using EShop.Domain.Email;

namespace EShop.Api.IntegrationTests;

internal sealed class RecordingEmailSender : IEmailSender
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    private readonly Channel<EmailMessage> _sent = Channel.CreateUnbounded<EmailMessage>();
    private readonly TaskCompletionSource _firstSendStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _calls;

    public string? FailSubject { get; init; }

    public Task? FirstSendGate { get; init; }

    public Task FirstSendStarted => _firstSendStarted.Task.WaitAsync(WaitLimit);

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref _calls) == 1)
        {
            _firstSendStarted.TrySetResult();

            if (FirstSendGate is not null)
            {
                await FirstSendGate;
            }
        }

        if (message.Subject == FailSubject)
        {
            throw new InvalidOperationException("SMTP server is not available");
        }

        await _sent.Writer.WriteAsync(message, cancellationToken);
    }

    public async Task<EmailMessage> NextSentAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(WaitLimit);
        return await _sent.Reader.ReadAsync(timeout.Token);
    }

    public bool HasSentMessage => _sent.Reader.TryPeek(out _);
}