using EShop.Domain.Orders;
using EShop.Domain.Payments;
using EShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EShop.Infrastructure.Payments;

public sealed partial class StripeWebhookService(
    AppDbContext db, IOptions<StripeOptions> options, TimeProvider time, ILogger<StripeWebhookService> logger)
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
        var now = time.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var paymentChanged = await db.Payments
            .Where(p => p.Id == paymentId && p.Status == PaymentStatus.Pending)
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
            LogLatePayment(logger, orderId, paymentId); // handled in T5.4
        return PaymentWebhookResult.Processed;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stripe webhook with an invalid signature")]
    private static partial void LogInvalidSignature(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stripe webhook for an unknown session {SessionId}")]
    private static partial void LogUnknownSession(ILogger logger, string sessionId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Payment {PaymentId} was paid, but order {OrderId} is no longer PendingPayment")]
    private static partial void LogLatePayment(ILogger logger, Guid orderId, Guid paymentId);
}