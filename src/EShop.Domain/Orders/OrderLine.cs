namespace EShop.Domain.Orders;

public sealed record OrderLine(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity);