using System.Net;
using EShop.Domain.Payments;
using Xunit;

namespace EShop.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class AdminReturnRefundTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Receive_returns_the_stock_of_the_returned_lines()
    {
        var (admin, order, returnId) = await PrepareReturnAsync(quantity: 2, receive: false);
        using var _ = admin;
        Assert.Equal(3, await CheckoutTestData.StockOfAsync(factory, order.ProductId));

        using var response = await ReturnTestData.ActAsync(admin, returnId, "receive");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(5, await CheckoutTestData.StockOfAsync(factory, order.ProductId));
    }

    [Fact]
    public async Task Full_return_refunds_the_whole_payment_through_the_gateway()
    {
        var (admin, order, returnId) = await PrepareReturnAsync(quantity: 2, receive: true);
        using var _ = admin;

        using var response = await ReturnTestData.ActAsync(admin, returnId, "refund");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var request = await ReturnTestData.GetAsync(admin, returnId);
        var payment = await WebhookTestData.GetPaymentAsync(factory, order.OrderId);
        var refund = Assert.Single(factory.Payments.Refunds);
        Assert.Equal("Refunded", request.Status);
        Assert.Equal(request.RefundAmount, refund.Amount);
        Assert.Equal($"return-refund-{returnId}", refund.IdempotencyKey);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(payment.Amount, payment.RefundedAmount);
    }

    [Fact]
    public async Task Partial_return_refund_keeps_the_payment_paid()
    {
        var (admin, order, returnId) = await PrepareReturnAsync(quantity: 1, receive: true);
        using var _ = admin;

        using var response = await ReturnTestData.ActAsync(admin, returnId, "refund");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var request = await ReturnTestData.GetAsync(admin, returnId);
        var payment = await WebhookTestData.GetPaymentAsync(factory, order.OrderId);
        Assert.Equal("Refunded", request.Status);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Equal(request.RefundAmount, payment.RefundedAmount);
        Assert.True(payment.RefundedAmount < payment.Amount);
    }

    [Fact]
    public async Task Repeated_refund_returns_200_without_a_second_gateway_call()
    {
        var (admin, order, returnId) = await PrepareReturnAsync(quantity: 1, receive: true);
        using var _ = admin;

        using var first = await ReturnTestData.ActAsync(admin, returnId, "refund");
        using var second = await ReturnTestData.ActAsync(admin, returnId, "refund");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Single(factory.Payments.Refunds);
        var payment = await WebhookTestData.GetPaymentAsync(factory, order.OrderId);
        Assert.Equal(payment.RefundedAmount, (await ReturnTestData.GetAsync(admin, returnId)).RefundAmount);
    }

    [Fact]
    public async Task Provider_failure_gives_502_keeps_the_return_received_and_retry_works()
    {
        var (admin, order, returnId) = await PrepareReturnAsync(quantity: 1, receive: true);
        using var _ = admin;
        factory.Payments.FailRefund = true;

        using var failed = await ReturnTestData.ActAsync(admin, returnId, "refund");

        Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
        Assert.Equal("Received", (await ReturnTestData.GetAsync(admin, returnId)).Status);
        var untouched = await WebhookTestData.GetPaymentAsync(factory, order.OrderId);
        Assert.Equal(0m, untouched.RefundedAmount);

        factory.Payments.FailRefund = false;
        using var retry = await ReturnTestData.ActAsync(admin, returnId, "refund");

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal("Refunded", (await ReturnTestData.GetAsync(admin, returnId)).Status);
        Assert.Single(factory.Payments.Refunds);
    }

    [Fact]
    public async Task Refund_before_receive_gives_409_and_calls_no_gateway()
    {
        var (admin, _, returnId) = await PrepareReturnAsync(quantity: 1, receive: false);
        using var _ = admin;

        using var response = await ReturnTestData.ActAsync(admin, returnId, "refund");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(factory.Payments.Refunds);
    }

    private async Task<(HttpClient Admin, DeliveredOrder Order, Guid ReturnId)> PrepareReturnAsync(
        int quantity, bool receive)
    {
        var adminUser = await factory.RegisterAdminAndLoginAsync();
        var admin = factory.CreateAuthorizedClient(adminUser);
        var order = await ReturnTestData.CreateDeliveredOrderAsync(factory, admin);
        var returnId = await ReturnTestData.RequestReturnAsync(order, quantity);

        using var approve = await ReturnTestData.ActAsync(admin, returnId, "approve");
        approve.EnsureSuccessStatusCode();
        if (receive)
        {
            using var received = await ReturnTestData.ActAsync(admin, returnId, "receive");
            received.EnsureSuccessStatusCode();
        }

        return (admin, order, returnId);
    }
}