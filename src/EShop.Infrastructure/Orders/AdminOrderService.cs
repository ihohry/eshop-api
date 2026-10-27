using EShop.Domain.Orders;
using EShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EShop.Infrastructure.Orders;

public sealed class AdminOrderService(AppDbContext db, TimeProvider time) : IAdminOrderService
{
    public async Task<AdminOrderPage> ListAsync(
        OrderStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking();
        if (status is { } s)
            query = query.Where(o => o.Status == s);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new AdminOrderSummary(
                o.Id, o.UserId, o.Status, o.Currency, o.Total, o.Items.Count, o.CreatedAt))
            .ToListAsync(ct);
        return new AdminOrderPage(items, page, pageSize, total);
    }

    public Task<Order?> GetAsync(Guid orderId, CancellationToken ct) =>
        db.Orders.AsNoTracking().Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

    public async Task<AdminOrderChangeResult> ChangeStatusAsync(
        Guid orderId, AdminOrderAction action, CancellationToken ct)
    {
        var order = await GetAsync(orderId, ct);
        if (order is null) return new(AdminOrderChangeStatus.NotFound);
        if (!CanApply(action, order)) return Classify(action, order);

        var now = time.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var changed = await ApplyAsync(orderId, action, now, ct);
        if (changed == 0)
        {
            // A parallel request was faster: report the current state.
            await tx.RollbackAsync(ct);
            var current = await GetAsync(orderId, ct);
            return current is null
                ? new(AdminOrderChangeStatus.NotFound)
                : Classify(action, current);
        }

        if (action is AdminOrderAction.Cancel or AdminOrderAction.Refuse)
        {
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
        }

        await tx.CommitAsync(ct);
        return new(AdminOrderChangeStatus.Changed, await GetAsync(orderId, ct));
    }

    private static bool CanApply(AdminOrderAction action, Order order) => action switch
    {
        AdminOrderAction.MarkPaid => order.Status == OrderStatus.PendingPayment,
        AdminOrderAction.Confirm => order.Status == OrderStatus.PendingConfirmation,
        AdminOrderAction.Ship => order.Status is OrderStatus.Paid or OrderStatus.Confirmed,
        AdminOrderAction.Deliver => order.Status == OrderStatus.Shipped,
        AdminOrderAction.Cancel => order.Status is OrderStatus.PendingPayment or OrderStatus.Paid
            or OrderStatus.PendingConfirmation or OrderStatus.Confirmed,
        AdminOrderAction.Refuse => order.PaymentMethod == PaymentMethod.PayOnReceipt
            && order.Status is OrderStatus.PendingConfirmation or OrderStatus.Confirmed or OrderStatus.Shipped,
        _ => false,
    };

    private static AdminOrderChangeResult Classify(AdminOrderAction action, Order order) =>
        (action == AdminOrderAction.Cancel && order.Status == OrderStatus.Cancelled)
        || (action == AdminOrderAction.Refuse && order.Status == OrderStatus.Refused)
            ? new(AdminOrderChangeStatus.AlreadyDone, order)
            : new(AdminOrderChangeStatus.InvalidTransition, order);

    // Each change is one conditional UPDATE: only one caller can win.
    private async Task<int> ApplyAsync(
        Guid orderId, AdminOrderAction action, DateTimeOffset now, CancellationToken ct)
    {
        var orders = db.Orders.Where(o => o.Id == orderId);
        return action switch
        {
            AdminOrderAction.MarkPaid => await orders
                .Where(o => o.Status == OrderStatus.PendingPayment)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Status, OrderStatus.Paid)
                    .SetProperty(o => o.PaidAt, now), ct),
            AdminOrderAction.Confirm => await orders
                .Where(o => o.Status == OrderStatus.PendingConfirmation)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Status, OrderStatus.Confirmed)
                    .SetProperty(o => o.ConfirmedAt, now), ct),
            AdminOrderAction.Ship => await orders
                .Where(o => o.Status == OrderStatus.Paid || o.Status == OrderStatus.Confirmed)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Status, OrderStatus.Shipped)
                    .SetProperty(o => o.ShippedAt, now), ct),
            AdminOrderAction.Deliver => await orders
                .Where(o => o.Status == OrderStatus.Shipped)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Status, OrderStatus.Delivered)
                    .SetProperty(o => o.DeliveredAt, now)
                    .SetProperty(o => o.PaidAt, o =>
                        o.PaymentMethod == PaymentMethod.PayOnReceipt ? now : o.PaidAt), ct),
            AdminOrderAction.Cancel => await orders
                .Where(o => o.Status == OrderStatus.PendingPayment || o.Status == OrderStatus.Paid
                    || o.Status == OrderStatus.PendingConfirmation || o.Status == OrderStatus.Confirmed)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Status, OrderStatus.Cancelled)
                    .SetProperty(o => o.CancelledAt, now), ct),
            AdminOrderAction.Refuse => await orders
                .Where(o => o.PaymentMethod == PaymentMethod.PayOnReceipt
                    && (o.Status == OrderStatus.PendingConfirmation || o.Status == OrderStatus.Confirmed
                        || o.Status == OrderStatus.Shipped))
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Refused), ct),
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }
}