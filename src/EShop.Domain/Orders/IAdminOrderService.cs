namespace EShop.Domain.Orders;

public enum AdminOrderAction { MarkPaid, Ship, Deliver, Cancel }

public enum AdminOrderChangeStatus { Changed, AlreadyDone, NotFound, InvalidTransition }

public sealed record AdminOrderChangeResult(AdminOrderChangeStatus Status, Order? Order = null);

public sealed record AdminOrderSummary(
    Guid Id, Guid UserId, OrderStatus Status, string Currency, decimal Total,
    int ItemCount, DateTimeOffset CreatedAt);

public sealed record AdminOrderPage(
    IReadOnlyList<AdminOrderSummary> Items, int Page, int PageSize, int TotalCount);

public interface IAdminOrderService
{
    Task<AdminOrderPage> ListAsync(OrderStatus? status, int page, int pageSize, CancellationToken ct);
    Task<Order?> GetAsync(Guid orderId, CancellationToken ct);
    Task<AdminOrderChangeResult> ChangeStatusAsync(Guid orderId, AdminOrderAction action, CancellationToken ct);
}