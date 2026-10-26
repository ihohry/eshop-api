namespace EShop.Domain.Orders;

public interface ICheckoutService
{
    Task<CheckoutResult> CheckoutAsync(
    Guid userId, ShippingAddress address, DeliveryMethod method,
    PickupPoint? pickupPoint, PaymentMethod paymentMethod, CancellationToken ct);
}