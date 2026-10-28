namespace EShop.Domain.Returns;

public interface IReturnService
{
    Task<ReturnRequest> CreateAsync(Guid userId, Guid orderId, string reason,
        IReadOnlyCollection<ReturnLine> lines, CancellationToken ct);

    Task<IReadOnlyList<ReturnRequest>> ListForOrderAsync(Guid userId, Guid orderId, CancellationToken ct);
}