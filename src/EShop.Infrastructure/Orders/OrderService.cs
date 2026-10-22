using EShop.Domain.Orders;
using EShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EShop.Infrastructure.Orders;

public sealed class OrderService(AppDbContext db, TimeProvider time) : IOrderService
{
    public async Task<OrderPage> ListAsync(Guid userId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking().Where(o => o.UserId == userId);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new OrderSummary(o.Id, o.Status, o.Currency, o.Total, o.Items.Count, o.CreatedAt))
            .ToListAsync(ct);
        return new OrderPage(items, page, pageSize, total);
    }

    public Task<Order?> GetAsync(Guid userId, Guid orderId, CancellationToken ct) =>
        LoadAsync(userId, orderId, ct);

    public async Task<CancelOrderResult> CancelAsync(Guid userId, Guid orderId, CancellationToken ct)
    {
        var order = await LoadAsync(userId, orderId, ct);
        if (order is null) return new CancelOrderResult(CancelOrderStatus.NotFound);
        if (order.Status != OrderStatus.PendingPayment) return Classify(order);

        var now = time.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Conditional status change (same idea as the expiry job in T4.5): only one caller can win.
        var changed = await db.Orders
            .Where(o => o.Id == orderId && o.Status == OrderStatus.PendingPayment)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OrderStatus.Cancelled)
                .SetProperty(o => o.CancelledAt, now), ct);

        if (changed == 0)
        {
            // A parallel cancel or expiry was faster: report the current state.
            await tx.RollbackAsync(ct);
            var current = await LoadAsync(userId, orderId, ct);
            return current is null
                ? new CancelOrderResult(CancelOrderStatus.NotFound)
                : Classify(current);
        }

        // Same product order as in checkout, so transactions cannot block each other in a cycle.
        foreach (var item in order.Items.OrderBy(i => i.ProductId))
        {
            var productId = item.ProductId;
            var quantity = item.Quantity;
            await db.Products
                .Where(p => p.Id == productId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.StockQuantity, p => p.StockQuantity + quantity)
                    .SetProperty(p => p.UpdatedAt, now), ct);
        }

        await tx.CommitAsync(ct);
        return new CancelOrderResult(CancelOrderStatus.Cancelled, await LoadAsync(userId, orderId, ct));
    }

    private static CancelOrderResult Classify(Order order) =>
        order.Status == OrderStatus.Cancelled
            ? new CancelOrderResult(CancelOrderStatus.AlreadyCancelled, order)
            : new CancelOrderResult(CancelOrderStatus.NotCancellable, order);

    private Task<Order?> LoadAsync(Guid userId, Guid orderId, CancellationToken ct) =>
        db.Orders.AsNoTracking().Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct);
}