namespace EShop.Domain.Returns;

public sealed record ReturnLine(Guid ProductId, int Quantity);