namespace EShop.Domain.Orders;

public interface IOrderService
{
    Task<OrderPage> ListAsync(Guid userId, int page, int pageSize, CancellationToken ct);
    Task<Order?> GetAsync(Guid userId, Guid orderId, CancellationToken ct);
    Task<CancelOrderResult> CancelAsync(Guid userId, Guid orderId, CancellationToken ct);
}