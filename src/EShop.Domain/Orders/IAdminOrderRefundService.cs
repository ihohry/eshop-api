namespace EShop.Domain.Orders;

public enum AdminOrderRefundStatus { Refunded, AlreadyDone, NotFound, NotRefundable, GatewayFailed }

public sealed record AdminOrderRefundResult(AdminOrderRefundStatus Status, Order? Order = null);

public interface IAdminOrderRefundService
{
    Task<AdminOrderRefundResult> RefundAsync(Guid orderId, CancellationToken ct);
}