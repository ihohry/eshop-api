namespace EShop.Domain.Returns;

public enum AdminReturnAction { Approve, Reject, MarkReceived, MarkRefunded }

public enum AdminReturnChangeStatus { Changed, AlreadyDone, NotFound, InvalidTransition }

public sealed record AdminReturnChangeResult(AdminReturnChangeStatus Status, ReturnRequest? Return = null);

public sealed record AdminReturnSummary(
    Guid Id, Guid OrderId, Guid UserId, ReturnStatus Status,
    decimal RefundAmount, int ItemCount, DateTimeOffset CreatedAt);

public sealed record AdminReturnPage(
    IReadOnlyList<AdminReturnSummary> Items, int Page, int PageSize, int Total);

public interface IAdminReturnService
{
    Task<AdminReturnPage> ListAsync(ReturnStatus? status, int page, int pageSize, CancellationToken ct);

    Task<ReturnRequest?> GetAsync(Guid returnId, CancellationToken ct);

    Task<AdminReturnChangeResult> ChangeStatusAsync(
        Guid returnId, AdminReturnAction action, string? reason, CancellationToken ct);
}