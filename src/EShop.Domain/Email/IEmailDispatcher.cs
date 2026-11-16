namespace EShop.Domain.Email;

public interface IEmailDispatcher
{
    ValueTask DispatchAsync(EmailMessage message, CancellationToken cancellationToken = default);
}