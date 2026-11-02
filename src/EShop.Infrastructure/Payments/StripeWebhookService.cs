using EShop.Domain.Orders;
using EShop.Domain.Payments;
using EShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EShop.Infrastructure.Payments;

public sealed partial class StripeWebhookService(
    AppDbContext db, IOptions<StripeOptions> options, TimeProvider time, IPaymentGateway gateway, ILogger<StripeWebhookService> logger)
    : IPaymentWebhookService
{
    public async Task<PaymentWebhookResult> HandleAsync(
        string payload, string signatureHeader, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
            return PaymentWebhookResult.InvalidSignature;

        Stripe.Event stripeEvent;
        try
        {
            stripeEvent = Stripe.EventUtility.ConstructEvent(
                payload, signatureHeader, options.Value.WebhookSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (Stripe.StripeException)
        {
            LogInvalidSignature(logger);
            return PaymentWebhookResult.InvalidSignature;
        }

        if (stripeEvent.Type != Stripe.EventTypes.CheckoutSessionCompleted
            || stripeEvent.Data.Object is not Stripe.Checkout.Session session
            || session.PaymentStatus != "paid"
            || string.IsNullOrEmpty(session.PaymentIntentId))
        {
            return PaymentWebhookResult.Ignored;
        }

        var sessionId = session.Id;
        var paymentIntentId = session.PaymentIntentId;
        var payment = await db.Payments.AsNoTracking().FirstOrDefaultAsync(
            p => p.Provider == PaymentProvider.Stripe && p.ProviderSessionId == sessionId, ct);
        if (payment is null)
        {
            LogUnknownSession(logger, sessionId);
            return PaymentWebhookResult.Ignored;
        }

        var paymentId = payment.Id;
        var orderId = payment.OrderId;
        var amount = payment.Amount;
        var now = time.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var paymentChanged = await db.Payments
            .Where(p => p.Id == paymentId
                && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Expired))
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, PaymentStatus.Paid)
                .SetProperty(p => p.PaidAt, now)
                .SetProperty(p => p.ProviderPaymentReference, paymentIntentId), ct);
        if (paymentChanged == 0)
        {
            // Repeated event, or the payment is no longer Pending.
            await tx.RollbackAsync(ct);
            return PaymentWebhookResult.Ignored;
        }

        var orderChanged = await db.Orders
            .Where(o => o.Id == orderId && o.Status == OrderStatus.PendingPayment)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OrderStatus.Paid)
                .SetProperty(o => o.PaidAt, now), ct);
        await tx.CommitAsync(ct);

        if (orderChanged == 0)
            await RefundLatePaymentAsync(orderId, paymentId, paymentIntentId, amount, ct);
        return PaymentWebhookResult.Processed;
    }

    private async Task RefundLatePaymentAsync(
        Guid orderId, Guid paymentId, string paymentIntentId, decimal amount, CancellationToken ct)
    {
        try
        {
            await gateway.RefundAsync(paymentIntentId, amount, $"late-payment-{paymentId}", ct);
            await db.Payments
                .Where(p => p.Id == paymentId && p.Status == PaymentStatus.Paid)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Status, PaymentStatus.Refunded)
                    .SetProperty(p => p.RefundedAmount, p => p.Amount), ct);
            LogLatePaymentRefunded(logger, orderId, paymentId);
        }
        catch (PaymentGatewayException)
        {
            LogLateRefundFailed(logger, orderId, paymentId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stripe webhook with an invalid signature")]
    private static partial void LogInvalidSignature(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stripe webhook for an unknown session {SessionId}")]
    private static partial void LogUnknownSession(ILogger logger, string sessionId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Late payment {PaymentId} for order {OrderId} was refunded automatically")]
    private static partial void LogLatePaymentRefunded(ILogger logger, Guid orderId, Guid paymentId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Late payment {PaymentId} for order {OrderId} could NOT be refunded: refund it manually in the Stripe Dashboard")]
    private static partial void LogLateRefundFailed(ILogger logger, Guid orderId, Guid paymentId);
}