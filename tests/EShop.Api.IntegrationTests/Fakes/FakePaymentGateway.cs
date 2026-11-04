using System.Collections.Concurrent;
using EShop.Domain.Payments;

namespace EShop.Api.IntegrationTests.Fakes;

public sealed record RefundCall(string PaymentIntentId, decimal Amount, string IdempotencyKey);

public sealed class FakePaymentGateway : IPaymentGateway
{
    private readonly ConcurrentQueue<PaymentSessionRequest> _sessions = new();
    private readonly ConcurrentQueue<string> _expired = new();
    private readonly ConcurrentQueue<RefundCall> _refunds = new();
    private int _counter;

    public bool FailCreate { get; set; }

    public bool FailRefund { get; set; }

    public IReadOnlyList<PaymentSessionRequest> CreatedSessions => _sessions.ToArray();

    public IReadOnlyList<string> ExpiredSessions => _expired.ToArray();

    public IReadOnlyList<RefundCall> Refunds => _refunds.ToArray();

    public Task<HostedPaymentSession> CreateSessionAsync(PaymentSessionRequest request, CancellationToken ct)
    {
        if (FailCreate)
        {
            return Task.FromException<HostedPaymentSession>(new PaymentGatewayException("Fake gateway failure."));
        }

        _sessions.Enqueue(request);
        var id = $"cs_test_{Interlocked.Increment(ref _counter)}";
        return Task.FromResult(new HostedPaymentSession(id, $"https://test.local/pay/{id}", request.ExpiresAt));
    }

    public Task ExpireSessionAsync(string sessionId, CancellationToken ct)
    {
        _expired.Enqueue(sessionId);
        return Task.CompletedTask;
    }

    public Task RefundAsync(string paymentIntentId, decimal amount, string idempotencyKey, CancellationToken ct)
    {
        if (FailRefund)
        {
            return Task.FromException(new PaymentGatewayException("Fake gateway failure."));
        }

        _refunds.Enqueue(new RefundCall(paymentIntentId, amount, idempotencyKey));
        return Task.CompletedTask;
    }

    public Task<bool> RefundExistsAsync(string paymentIntentId, string refundKey, CancellationToken ct) =>
        Task.FromResult(_refunds.Any(r => r.PaymentIntentId == paymentIntentId && r.IdempotencyKey == refundKey));

    public void Reset()
    {
        _sessions.Clear();
        _expired.Clear();
        _refunds.Clear();
        FailCreate = false;
        FailRefund = false;
    }
}