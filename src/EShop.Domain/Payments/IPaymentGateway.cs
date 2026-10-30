namespace EShop.Domain.Payments;

public interface IPaymentGateway
{
    Task<HostedPaymentSession> CreateSessionAsync(PaymentSessionRequest request, CancellationToken ct);
    Task ExpireSessionAsync(string sessionId, CancellationToken ct);
}