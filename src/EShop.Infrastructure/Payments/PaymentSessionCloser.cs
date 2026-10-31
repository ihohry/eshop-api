using EShop.Domain.Payments;
using EShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EShop.Infrastructure.Payments;

public sealed partial class PaymentSessionCloser(
    AppDbContext db, IPaymentGateway gateway, ILogger<PaymentSessionCloser> logger)
    : IPaymentSessionCloser
{
    public async Task CloseForOrderAsync(Guid orderId, CancellationToken ct)
    {
        try
        {
            var pending = await db.Payments.AsNoTracking()
                .Where(p => p.OrderId == orderId && p.Status == PaymentStatus.Pending)
                .Select(p => new { p.Id, p.ProviderSessionId })
                .ToListAsync(ct);

            foreach (var item in pending)
            {
                var id = item.Id;
                var changed = await db.Payments
                    .Where(p => p.Id == id && p.Status == PaymentStatus.Pending)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentStatus.Expired), ct);
                if (changed == 0) continue;

                try
                {
                    await gateway.ExpireSessionAsync(item.ProviderSessionId, ct);
                }
                catch (PaymentGatewayException)
                {
                    LogExpireFailed(logger, orderId, id); // the link expires by itself anyway
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogCloseFailed(logger, ex, orderId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Could not expire the Stripe session of payment {PaymentId} (order {OrderId})")]
    private static partial void LogExpireFailed(ILogger logger, Guid orderId, Guid paymentId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not close the payment link of order {OrderId}")]
    private static partial void LogCloseFailed(ILogger logger, Exception exception, Guid orderId);
}