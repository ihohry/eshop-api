namespace EShop.Domain.Payments;

public enum PaymentWebhookResult
{
    Processed,
    Ignored,
    InvalidSignature,
}

public interface IPaymentWebhookService
{
    Task<PaymentWebhookResult> HandleAsync(string payload, string signatureHeader, CancellationToken ct);
}