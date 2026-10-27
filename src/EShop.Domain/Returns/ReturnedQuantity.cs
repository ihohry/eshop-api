namespace EShop.Domain.Returns;

public sealed record ReturnedQuantity(Guid ProductId, int Quantity);