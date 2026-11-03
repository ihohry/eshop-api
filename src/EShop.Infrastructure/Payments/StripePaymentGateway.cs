using EShop.Domain.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace EShop.Infrastructure.Payments;

public sealed partial class StripePaymentGateway(
    IOptions<StripeOptions> options, ILogger<StripePaymentGateway> logger) : IPaymentGateway
{
    private const string RefundKeyMetadata = "refund_key";

    private readonly StripeOptions _options = options.Value;
    private readonly SessionService _sessions = new(new StripeClient(options.Value.SecretKey));
    private readonly RefundService _refunds = new(new StripeClient(options.Value.SecretKey));

    public async Task<HostedPaymentSession> CreateSessionAsync(
        PaymentSessionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var orderId = request.OrderId.ToString();
        var metadata = new Dictionary<string, string> { ["order_id"] = orderId };

        var create = new SessionCreateOptions
        {
            Mode = "payment",
            ClientReferenceId = orderId,
            SuccessUrl = Fill(_options.SuccessUrlTemplate, orderId),
            CancelUrl = Fill(_options.CancelUrlTemplate, orderId),
            ExpiresAt = request.ExpiresAt.UtcDateTime,
            Metadata = metadata,
            PaymentIntentData = new SessionPaymentIntentDataOptions { Metadata = metadata },
            LineItems = request.Lines.Select(l => new SessionLineItemOptions
            {
                Quantity = l.Quantity,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = request.Currency.ToLowerInvariant(),
                    UnitAmount = ToMinorUnits(l.UnitPrice),
                    ProductData = new SessionLineItemPriceDataProductDataOptions { Name = l.Name },
                },
            }).ToList(),
        };

        try
        {
            var session = await _sessions.CreateAsync(create, cancellationToken: ct);
            if (string.IsNullOrWhiteSpace(session.Url))
                throw new PaymentGatewayException("The payment provider returned no payment link.");

            return new HostedPaymentSession(
                session.Id,
                session.Url,
                new DateTimeOffset(DateTime.SpecifyKind(session.ExpiresAt, DateTimeKind.Utc)));
        }
        catch (StripeException ex)
        {
            LogSessionFailed(logger, (int)ex.HttpStatusCode, ex.StripeError?.Code);
            throw new PaymentGatewayException("The payment provider request failed.", ex);
        }
    }

    public async Task ExpireSessionAsync(string sessionId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        try
        {
            await _sessions.ExpireAsync(sessionId, cancellationToken: ct);
        }
        catch (StripeException ex)
        {
            LogExpireFailed(logger, (int)ex.HttpStatusCode, ex.StripeError?.Code);
            throw new PaymentGatewayException("The payment provider request failed.", ex);
        }
    }

    public async Task RefundAsync(
    string paymentIntentId, decimal amount, string idempotencyKey, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentIntentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        try
        {
            await _refunds.CreateAsync(
                new RefundCreateOptions
                {
                    PaymentIntent = paymentIntentId,
                    Amount = ToMinorUnits(amount),
                    Metadata = new Dictionary<string, string> { [RefundKeyMetadata] = idempotencyKey },
                },
                new RequestOptions { IdempotencyKey = idempotencyKey }, ct);
        }
        catch (StripeException ex)
        {
            LogRefundFailed(logger, (int)ex.HttpStatusCode, ex.StripeError?.Code);
            throw new PaymentGatewayException("The payment provider request failed.", ex);
        }
    }

    public async Task<bool> RefundExistsAsync(
    string paymentIntentId, string refundKey, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentIntentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(refundKey);
        try
        {
            var list = new RefundListOptions { PaymentIntent = paymentIntentId, Limit = 100 };
            await foreach (var refund in _refunds.ListAutoPagingAsync(list, cancellationToken: ct))
            {
                if (refund.Status is "failed" or "canceled") continue;
                if (refund.Metadata is not null
                    && refund.Metadata.TryGetValue(RefundKeyMetadata, out var key)
                    && key == refundKey)
                    return true;
            }
            return false;
        }
        catch (StripeException ex)
        {
            LogRefundLookupFailed(logger, (int)ex.HttpStatusCode, ex.StripeError?.Code);
            throw new PaymentGatewayException("The payment provider request failed.", ex);
        }
    }

    private static string Fill(string template, string orderId) =>
        template.Replace("{orderId}", orderId, StringComparison.Ordinal);

    private static long ToMinorUnits(decimal amount) =>
        decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Stripe session creation failed: HTTP {Status}, code {Code}")]
    private static partial void LogSessionFailed(ILogger logger, int status, string? code);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Stripe session expiry failed: HTTP {Status}, code {Code}")]
    private static partial void LogExpireFailed(ILogger logger, int status, string? code);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Stripe refund failed: HTTP {Status}, code {Code}")]
    private static partial void LogRefundFailed(ILogger logger, int status, string? code);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Stripe refund lookup failed: HTTP {Status}, code {Code}")]
    private static partial void LogRefundLookupFailed(ILogger logger, int status, string? code);
}