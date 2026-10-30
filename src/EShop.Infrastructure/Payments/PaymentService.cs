using EShop.Domain.Orders;
using EShop.Domain.Payments;
using EShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EShop.Infrastructure.Payments;

public sealed partial class PaymentService(
    AppDbContext db, IPaymentGateway gateway, TimeProvider time, ILogger<PaymentService> logger)
    : IPaymentService
{
    private static readonly TimeSpan MaxLinkLifetime = TimeSpan.FromHours(23);
    private static readonly TimeSpan MinTimeLeft = TimeSpan.FromMinutes(31);

    public async Task<StartPaymentResult> StartAsync(Guid userId, Guid orderId, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct);
        if (order is null)
            return new StartPaymentResult(StartPaymentStatus.NotFound);
        if (order.Status != OrderStatus.PendingPayment || order.PaymentMethod != PaymentMethod.Prepaid)
            return new StartPaymentResult(StartPaymentStatus.NotPayable);

        var now = time.GetUtcNow();
        if (order.ExpiresAt - now < MinTimeLeft)
            return new StartPaymentResult(StartPaymentStatus.ExpiringSoon);

        var pending = await db.Payments.FirstOrDefaultAsync(
            p => p.OrderId == orderId && p.Status == PaymentStatus.Pending, ct);
        if (pending is not null)
        {
            if (pending.ExpiresAt > now)
                return new StartPaymentResult(StartPaymentStatus.Started, pending);

            // The old link has run out; free the "one Pending payment" slot first.
            pending.MarkExpired();
            await db.SaveChangesAsync(ct);
        }

        var linkEnd = now + MaxLinkLifetime;
        var expiresAt = order.ExpiresAt < linkEnd ? order.ExpiresAt : linkEnd;
        var lines = order.Items
            .Select(i => new PaymentLine(i.ProductName, i.UnitPrice, i.Quantity))
            .ToList();

        HostedPaymentSession session;
        try
        {
            session = await gateway.CreateSessionAsync(
                new PaymentSessionRequest(order.Id, order.Currency, lines, expiresAt), ct);
        }
        catch (PaymentGatewayException)
        {
            return new StartPaymentResult(StartPaymentStatus.ProviderUnavailable);
        }

        var payment = Payment.Create(order.Id, PaymentProvider.Stripe, session, order.Total, now);
        db.Payments.Add(payment);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A parallel request saved its payment first: close our extra session.
            await ExpireQuietlyAsync(session.Id, order.Id);
            return new StartPaymentResult(StartPaymentStatus.Conflict);
        }

        return new StartPaymentResult(StartPaymentStatus.Started, payment);
    }

    private async Task ExpireQuietlyAsync(string sessionId, Guid orderId)
    {
        try
        {
            await gateway.ExpireSessionAsync(sessionId, CancellationToken.None);
        }
        catch (PaymentGatewayException ex)
        {
            LogExpireFailed(logger, ex, orderId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Could not expire the extra payment session of order {OrderId}")]
    private static partial void LogExpireFailed(ILogger logger, Exception exception, Guid orderId);
}