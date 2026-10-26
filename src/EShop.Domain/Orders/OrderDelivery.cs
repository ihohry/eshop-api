namespace EShop.Domain.Orders;

public sealed record OrderDelivery(DeliveryMethod Method, PickupPoint? PickupPoint);
