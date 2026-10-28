using EShop.Domain.Exceptions;
using EShop.Domain.Orders;
using EShop.Domain.Returns;
using EShop.Infrastructure.Orders;
using EShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EShop.Infrastructure.Returns;

public sealed class ReturnService(AppDbContext db, TimeProvider time, IOptions<OrderOptions> options)
    : IReturnService
{
    public async Task<ReturnRequest> CreateAsync(Guid userId, Guid orderId, string reason,
        IReadOnlyCollection<ReturnLine> lines, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Lock the order row: a parallel request for the same order waits here until we commit,
        // so it counts the quantities including our return.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT 1 FROM "Orders" WHERE "Id" = {orderId} AND "UserId" = {userId} FOR UPDATE""", ct);

        var order = await db.Orders.AsNoTracking().Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct)
            ?? throw new NotFoundException("Order not found.");

        if (order.Status != OrderStatus.Delivered)
            throw new ConflictException("Only delivered orders can be returned.");

        var now = time.GetUtcNow();
        if (order.ReturnableUntil(options.Value.ReturnWindow) is not { } until || now > until)
            throw new ConflictException("The return window has closed.");

        var alreadyReturned = await db.ReturnRequests
            .Where(r => r.OrderId == orderId && r.Status != ReturnStatus.Rejected)
            .SelectMany(r => r.Items)
            .Select(i => new ReturnedQuantity(i.ProductId, i.Quantity))
            .ToListAsync(ct);

        ReturnRequest request;
        try
        {
            request = ReturnRequest.Create(order, reason, lines, alreadyReturned, now, options.Value.ReturnWindow);
        }
        catch (ArgumentException ex)
        {
            throw new DomainException(WithoutParameterName(ex));
        }

        db.ReturnRequests.Add(request);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return request;
    }

    public async Task<IReadOnlyList<ReturnRequest>> ListForOrderAsync(
        Guid userId, Guid orderId, CancellationToken ct)
    {
        if (!await db.Orders.AnyAsync(o => o.Id == orderId && o.UserId == userId, ct))
            throw new NotFoundException("Order not found.");

        return await db.ReturnRequests.AsNoTracking().Include(r => r.Items)
            .Where(r => r.OrderId == orderId)
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .ToListAsync(ct);
    }

    // ArgumentException appends " (Parameter 'x')" to the message; the client should not see it.
    private static string WithoutParameterName(ArgumentException ex)
    {
        var index = ex.Message.IndexOf(" (Parameter", StringComparison.Ordinal);
        return index < 0 ? ex.Message : ex.Message[..index];
    }
}