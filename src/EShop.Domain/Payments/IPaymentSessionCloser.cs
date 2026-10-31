namespace EShop.Domain.Payments;

/// <summary>Closes the open payment link of an order that has ended. Never throws.</summary>
public interface IPaymentSessionCloser
{
    Task CloseForOrderAsync(Guid orderId, CancellationToken ct);
}