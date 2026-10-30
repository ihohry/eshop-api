using EShop.Domain.Catalog;

namespace EShop.Domain.Payments;

public sealed class Payment
{
    private Payment() {}

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public PaymentProvider Provider { get; private set; }
    public string ProviderSessionId { get; private set; } = string.Empty;
    public string? ProviderPaymentReference { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = CatalogDefaults.Currency;
    public PaymentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }

    public static Payment Create(
        Guid orderId,
        PaymentProvider provider,
        HostedPaymentSession session,
        decimal amount,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(session.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(session.Url);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        return new Payment
        {
            Id = Guid.CreateVersion7(),
            OrderId = orderId,
            Provider = provider,
            ProviderSessionId = session.Id,
            Url = session.Url,
            Amount = amount,
            Currency = CatalogDefaults.Currency,
            Status = PaymentStatus.Pending,
            CreatedAt = now,
            ExpiresAt = session.ExpiresAt,
        };
    }

    public void MarkPaid(string providerPaymentReference, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerPaymentReference);
        EnsureStatus(PaymentStatus.Pending);
        ProviderPaymentReference = providerPaymentReference;
        PaidAt = now;
        Status = PaymentStatus.Paid;
    }

    public void MarkExpired()
    {
        EnsureStatus(PaymentStatus.Pending);
        Status = PaymentStatus.Expired;
    }

    public void MarkRefunded()
    {
        EnsureStatus(PaymentStatus.Paid);
        Status = PaymentStatus.Refunded;
    }

    private void EnsureStatus(PaymentStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException(
                $"Payment is {Status}, but {expected} is required.");
        }
    }
}