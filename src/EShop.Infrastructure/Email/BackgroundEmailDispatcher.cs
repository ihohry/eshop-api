using System.Threading.Channels;
using EShop.Domain.Email;

namespace EShop.Infrastructure.Email;

public sealed class BackgroundEmailDispatcher : IEmailDispatcher
{
    public const int Capacity = 1000;

    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });

    public ChannelReader<EmailMessage> Reader => _channel.Reader;

    public ValueTask DispatchAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return _channel.Writer.WriteAsync(message, cancellationToken);
    }
}