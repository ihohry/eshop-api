using EShop.Domain.Orders;

namespace EShop.Api.Orders;

public sealed record OrderItemResponse(
    Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal);

public sealed record OrderResponse(
    Guid Id, string Status, string Currency, decimal Total,
    DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt,
    string DeliveryMethod, ShippingAddress ShippingAddress, PickupPoint? PickupPoint,
    IReadOnlyList<OrderItemResponse> Items);

public sealed record CheckoutProblemResponse(
    Guid ProductId, string ProductName, int Quantity, string Reason);

public sealed record OrderSummaryResponse(
    Guid Id, string Status, string Currency, decimal Total, int ItemCount, DateTimeOffset CreatedAt);

public sealed record OrderPageResponse(
    IReadOnlyList<OrderSummaryResponse> Items, int Page, int PageSize, int TotalCount);

public static class OrderMappings
{
    public static OrderResponse ToResponse(this Order o) => new(
        o.Id, o.Status.ToString(), o.Currency, o.Total, o.CreatedAt, o.ExpiresAt,
        o.DeliveryMethod.ToString(), o.ShippingAddress,
        o.PickupPointId is null ? null : new PickupPoint(o.PickupPointId, o.PickupPointName!),
        o.Items.Select(i => new OrderItemResponse(
            i.ProductId, i.ProductName, i.UnitPrice, i.Quantity, i.LineTotal)).ToList());
}