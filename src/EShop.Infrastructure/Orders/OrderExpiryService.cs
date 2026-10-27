using EShop.Domain.Orders;
using EShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EShop.Infrastructure.Orders;

public sealed partial class OrderExpiryService(
    AppDbContext db, TimeProvider time, ILogger<OrderExpiryService> logger)
{
    private const int BatchSize = 100;

    public async Task<int> ExpireDueAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var dueIds = await db.Orders.AsNoTracking()
            .Where(o => (o.Status == OrderStatus.PendingPayment
                || o.Status == OrderStatus.PendingConfirmation)
            && o.ExpiresAt <= now)
            .OrderBy(o => o.ExpiresAt)
            .Select(o => o.Id)
            .Take(BatchSize)
            .ToListAsync(ct);

        var expired = 0;
        foreach (var id in dueIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (await ExpireOneAsync(id, now, ct)) expired++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogExpireFailed(ex, id);
            }
        }
        return expired;
    }

    private async Task<bool> ExpireOneAsync(Guid orderId, DateTimeOffset now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Only one caller can win (cancel, admin cancel and expiry use the same idea).
        var changed = await db.Orders
            .Where(o => o.Id == orderId
                && (o.Status == OrderStatus.PendingPayment
                    || o.Status == OrderStatus.PendingConfirmation)
                && o.ExpiresAt <= now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OrderStatus.Expired)
                .SetProperty(o => o.CancelledAt, now), ct);

        if (changed == 0)
        {
            await tx.RollbackAsync(ct);
            return false;
        }

        var lines = await db.Orders
            .Where(o => o.Id == orderId)
            .SelectMany(o => o.Items)
            .Select(i => new { i.ProductId, i.Quantity })
            .OrderBy(i => i.ProductId) // same product order as checkout: no lock cycles
            .ToListAsync(ct);

        foreach (var line in lines)
        {
            var productId = line.ProductId;
            var quantity = line.Quantity;
            await db.Products
                .Where(p => p.Id == productId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.StockQuantity, p => p.StockQuantity + quantity)
                    .SetProperty(p => p.UpdatedAt, now), ct);
        }

        await tx.CommitAsync(ct);
        LogExpired(orderId);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderId} expired and its stock was released")]
    private partial void LogExpired(Guid orderId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not expire order {OrderId}")]
    private partial void LogExpireFailed(Exception exception, Guid orderId);
}