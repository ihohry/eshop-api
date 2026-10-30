namespace EShop.Domain.Payments;

public sealed record PaymentLine(string Name, decimal UnitPrice, int Quantity);

public sealed record PaymentSessionRequest(
    Guid OrderId, string Currency, IReadOnlyList<PaymentLine> Lines, DateTimeOffset ExpiresAt);