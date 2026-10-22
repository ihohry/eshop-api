namespace EShop.Domain.Orders;

public sealed record CancelOrderResult(CancelOrderStatus Status, Order? Order = null);