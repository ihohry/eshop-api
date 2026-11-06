using System.Net;
using System.Text.Json;
using EShop.Domain.Orders;
using EShop.Domain.Payments;
using Xunit;

namespace EShop.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class AdminOrderRefundTests(ApiFactory factory) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Refund_of_paid_order_cancels_it_and_refunds_the_full_amount()
    {
        var orderId = await CreatePaidOrderAsync();
        using var admin = await CreateAdminClientAsync();

        using var response = await admin.PostAsync($"/admin/orders/{orderId}/refund", null, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Cancelled", await ReadStatusAsync(response));
        var payment = await WebhookTestData.GetPaymentAsync(factory, orderId);
        var refund = Assert.Single(factory.Payments.Refunds);
        Assert.Equal(payment.Amount, refund.Amount);
        Assert.Equal($"order-refund-{payment.Id}", refund.IdempotencyKey);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(payment.Amount, payment.RefundedAmount);
    }

    [Fact]
    public async Task Repeated_refund_returns_200_without_a_second_refund()
    {
        var orderId = await CreatePaidOrderAsync();
        using var admin = await CreateAdminClientAsync();

        using var first = await admin.PostAsync($"/admin/orders/{orderId}/refund", null, Ct);
        using var second = await admin.PostAsync($"/admin/orders/{orderId}/refund", null, Ct);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Single(factory.Payments.Refunds);
    }

    [Fact]
    public async Task Provider_failure_gives_502_keeps_order_cancelled_and_retry_succeeds()
    {
        var orderId = await CreatePaidOrderAsync();
        using var admin = await CreateAdminClientAsync();
        factory.Payments.FailRefund = true;

        using var failed = await admin.PostAsync($"/admin/orders/{orderId}/refund", null, Ct);

        Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
        var order = await WebhookTestData.GetOrderAsync(factory, orderId);
        var payment = await WebhookTestData.GetPaymentAsync(factory, orderId);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Empty(factory.Payments.Refunds);

        factory.Payments.FailRefund = false;
        using var retry = await admin.PostAsync($"/admin/orders/{orderId}/refund", null, Ct);

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Single(factory.Payments.Refunds);
        payment = await WebhookTestData.GetPaymentAsync(factory, orderId);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
    }

    [Fact]
    public async Task Unpaid_order_gives_409_and_nothing_changes()
    {
        var user = await factory.RegisterAndLoginAsync();
        using var customer = factory.CreateAuthorizedClient(user);
        var pending = await WebhookTestData.CreatePendingPaymentAsync(factory, customer);
        using var admin = await CreateAdminClientAsync();

        using var response = await admin.PostAsync($"/admin/orders/{pending.OrderId}/refund", null, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var order = await WebhookTestData.GetOrderAsync(factory, pending.OrderId);
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Empty(factory.Payments.Refunds);
    }

    [Fact]
    public async Task Unknown_order_gives_404()
    {
        using var admin = await CreateAdminClientAsync();

        using var response = await admin.PostAsync($"/admin/orders/{Guid.NewGuid()}/refund", null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Customer_gets_403_and_anonymous_gets_401()
    {
        var orderId = await CreatePaidOrderAsync();
        var user = await factory.RegisterAndLoginAsync();
        using var customer = factory.CreateAuthorizedClient(user);
        using var anonymous = factory.CreateClient();

        using var forbidden = await customer.PostAsync($"/admin/orders/{orderId}/refund", null, Ct);
        using var unauthorized = await anonymous.PostAsync($"/admin/orders/{orderId}/refund", null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Empty(factory.Payments.Refunds);
    }

    private async Task<Guid> CreatePaidOrderAsync()
    {
        var user = await factory.RegisterAndLoginAsync();
        using var customer = factory.CreateAuthorizedClient(user);
        var pending = await WebhookTestData.CreatePendingPaymentAsync(factory, customer);
        var payload = WebhookTestData.CheckoutEvent(pending.SessionId);
        using var anonymous = factory.CreateClient();
        using var hook = await WebhookTestData.PostAsync(anonymous, payload, WebhookTestData.Sign(payload));
        hook.EnsureSuccessStatusCode();
        return pending.OrderId;
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var admin = await factory.RegisterAdminAndLoginAsync();
        return factory.CreateAuthorizedClient(admin);
    }

    private static async Task<string?> ReadStatusAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return json.RootElement.GetProperty("order").GetProperty("status").GetString();
    }
}