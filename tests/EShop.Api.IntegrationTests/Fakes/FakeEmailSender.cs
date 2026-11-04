using System.Collections.Concurrent;
using EShop.Domain.Email;

namespace EShop.Api.IntegrationTests.Fakes;

public sealed class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public IReadOnlyList<EmailMessage> Sent => _sent.ToArray();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public void Reset() => _sent.Clear();
}