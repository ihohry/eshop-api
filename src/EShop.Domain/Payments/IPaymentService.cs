namespace EShop.Domain.Payments;

public interface IPaymentService
{
    Task<StartPaymentResult> StartAsync(Guid userId, Guid orderId, CancellationToken ct);
}

public enum StartPaymentStatus
{
    Started,
    NotFound,
    NotPayable,
    ExpiringSoon,
    Conflict,
    ProviderUnavailable,
}

public sealed record StartPaymentResult(StartPaymentStatus Status, Payment? Payment = null);