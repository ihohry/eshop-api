namespace EShop.Domain.Orders;

public sealed record CheckoutProblem(
    Guid ProductId, string ProductName, int Quantity, CheckoutProblemReason Reason);