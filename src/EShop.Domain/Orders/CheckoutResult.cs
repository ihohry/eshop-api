namespace EShop.Domain.Orders;

public sealed record CheckoutResult(
    CheckoutStatus Status,
    Order? Order = null,
    IReadOnlyList<CheckoutProblem>? Problems = null);