using EShop.Domain.Orders;
using EShop.Domain.Payments;
using EShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EShop.Infrastructure.Orders;

public sealed partial class AdminOrderRefundService(
    AppDbContext db, IAdminOrderService orders, IPaymentGateway gateway,
    ILogger<AdminOrderRefundService> logger) : IAdminOrderRefundService
{
    public async Task<AdminOrderRefundResult> RefundAsync(Guid orderId, CancellationToken ct)
    {
        var order = await orders.GetAsync(orderId, ct);
        if (order is null) return new(AdminOrderRefundStatus.NotFound);

        var payment = await db.Payments.AsNoTracking()
            .Where(p => p.OrderId == orderId
                && (p.Status == PaymentStatus.Paid || p.Status == PaymentStatus.Refunded))
            .OrderByDescending(p => p.PaidAt)
            .FirstOrDefaultAsync(ct);

        // Checked before the cancel, so we never cancel an order that cannot be refunded.
        if (order.PaymentMethod != PaymentMethod.Prepaid
            || payment is null
            || string.IsNullOrEmpty(payment.ProviderPaymentReference)
            || order.Status is not (OrderStatus.Paid or OrderStatus.Cancelled))
        {
            return new(AdminOrderRefundStatus.NotRefundable, order);
        }

        if (payment.Status == PaymentStatus.Refunded)
            return new(AdminOrderRefundStatus.AlreadyDone, order);

        // Step 1: status and stock (skipped on a retry, when the order is already Cancelled).
        if (order.Status == OrderStatus.Paid)
        {
            var cancel = await orders.ChangeStatusAsync(orderId, AdminOrderAction.Cancel, ct);
            if (cancel.Status == AdminOrderChangeStatus.NotFound)
                return new(AdminOrderRefundStatus.NotFound);
            if (cancel.Status == AdminOrderChangeStatus.InvalidTransition)
                return new(AdminOrderRefundStatus.NotRefundable, cancel.Order);
        }

        // Step 2: the money. The same key on every retry, so Stripe never refunds twice.
        var paymentId = payment.Id;
        var refundKey = $"order-refund-{paymentId}";
        try
        {
            if (await gateway.RefundExistsAsync(payment.ProviderPaymentReference, refundKey, ct))
                LogRefundAlreadyAtProvider(logger, orderId, paymentId);
            else
                await gateway.RefundAsync(
                    payment.ProviderPaymentReference, payment.Amount - payment.RefundedAmount, refundKey, ct);
        }
        catch (PaymentGatewayException)
        {
            LogRefundFailed(logger, orderId, paymentId);
            return new(AdminOrderRefundStatus.GatewayFailed, await orders.GetAsync(orderId, ct));
        }

        await db.Payments
            .Where(p => p.Id == paymentId && p.Status == PaymentStatus.Paid)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, PaymentStatus.Refunded)
                .SetProperty(p => p.RefundedAmount, p => p.Amount), ct);
        LogRefunded(logger, orderId, paymentId);
        return new(AdminOrderRefundStatus.Refunded, await orders.GetAsync(orderId, ct));
    }

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Refund of payment {PaymentId} (order {OrderId}) failed; the order is Cancelled, repeat the request")]
    private static partial void LogRefundFailed(ILogger logger, Guid orderId, Guid paymentId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Payment {PaymentId} (order {OrderId}) was refunded")]
    private static partial void LogRefunded(ILogger logger, Guid orderId, Guid paymentId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Stripe already holds the refund of payment {PaymentId} (order {OrderId}); only the database is updated")]
    private static partial void LogRefundAlreadyAtProvider(ILogger logger, Guid orderId, Guid paymentId);
}