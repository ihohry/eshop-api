namespace EShop.Domain.Orders;

public sealed record OrderSummary(
    Guid Id, OrderStatus Status, string Currency, decimal Total, int ItemCount, DateTimeOffset CreatedAt);