namespace EShop.Domain.Payments;

public interface IPaymentGateway
{
    Task<HostedPaymentSession> CreateSessionAsync(PaymentSessionRequest request, CancellationToken ct);
    Task ExpireSessionAsync(string sessionId, CancellationToken ct);
    Task RefundAsync(string paymentIntentId, decimal amount, string idempotencyKey, CancellationToken ct);
    Task<bool> RefundExistsAsync(string paymentIntentId, string refundKey, CancellationToken ct);
}