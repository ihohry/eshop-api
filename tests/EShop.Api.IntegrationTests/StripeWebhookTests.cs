using System.Net;
using EShop.Domain.Orders;
using EShop.Domain.Payments;
using Xunit;

namespace EShop.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class StripeWebhookTests(ApiFactory factory) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Paid_event_marks_payment_and_order_as_paid()
    {
        var customer = await factory.RegisterAndLoginAsync();
        using var client = factory.CreateAuthorizedClient(customer);
        var pending = await WebhookTestData.CreatePendingPaymentAsync(factory, client);
        var payload = WebhookTestData.CheckoutEvent(pending.SessionId);

        using var response = await WebhookTestData.PostAsync(
            factory.CreateClient(), payload, WebhookTestData.Sign(payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await WebhookTestData.GetOrderAsync(factory, pending.OrderId);
        var payment = await WebhookTestData.GetPaymentAsync(factory, pending.OrderId);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.NotNull(order.PaidAt);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Equal($"pi_{pending.SessionId}", payment.ProviderPaymentReference);
        Assert.Empty(factory.Payments.Refunds);
    }

    [Fact]
    public async Task Invalid_signature_returns_400_and_changes_nothing()
    {
        var customer = await factory.RegisterAndLoginAsync();
        using var client = factory.CreateAuthorizedClient(customer);
        var pending = await WebhookTestData.CreatePendingPaymentAsync(factory, client);
        var payload = WebhookTestData.CheckoutEvent(pending.SessionId);

        using var response = await WebhookTestData.PostAsync(
            factory.CreateClient(), payload, "t=1700000000,v1=deadbeef");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var order = await WebhookTestData.GetOrderAsync(factory, pending.OrderId);
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
    }

    [Fact]
    public async Task Missing_signature_returns_400()
    {
        var payload = WebhookTestData.CheckoutEvent("cs_test_missing");

        using var response = await WebhookTestData.PostAsync(factory.CreateClient(), payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Repeated_event_returns_200_and_changes_nothing_more()
    {
        var customer = await factory.RegisterAndLoginAsync();
        using var client = factory.CreateAuthorizedClient(customer);
        var pending = await WebhookTestData.CreatePendingPaymentAsync(factory, client);
        var payload = WebhookTestData.CheckoutEvent(pending.SessionId);
        using var webhookClient = factory.CreateClient();

        using var first = await WebhookTestData.PostAsync(webhookClient, payload, WebhookTestData.Sign(payload));
        var paidAt = (await WebhookTestData.GetOrderAsync(factory, pending.OrderId)).PaidAt;
        using var second = await WebhookTestData.PostAsync(webhookClient, payload, WebhookTestData.Sign(payload));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var order = await WebhookTestData.GetOrderAsync(factory, pending.OrderId);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(paidAt, order.PaidAt);
        Assert.Empty(factory.Payments.Refunds);
    }

    [Fact]
    public async Task Unknown_session_returns_200_and_changes_nothing()
    {
        var customer = await factory.RegisterAndLoginAsync();
        using var client = factory.CreateAuthorizedClient(customer);
        var pending = await WebhookTestData.CreatePendingPaymentAsync(factory, client);
        var payload = WebhookTestData.CheckoutEvent("cs_test_unknown");

        using var response = await WebhookTestData.PostAsync(
            factory.CreateClient(), payload, WebhookTestData.Sign(payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await WebhookTestData.GetOrderAsync(factory, pending.OrderId);
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
    }

    [Theory]
    [InlineData("checkout.session.completed", "unpaid")]
    [InlineData("checkout.session.expired", "paid")]
    public async Task Ignored_events_return_200_and_change_nothing(string type, string paymentStatus)
    {
        var customer = await factory.RegisterAndLoginAsync();
        using var client = factory.CreateAuthorizedClient(customer);
        var pending = await WebhookTestData.CreatePendingPaymentAsync(factory, client);
        var payload = WebhookTestData.CheckoutEvent(pending.SessionId, paymentStatus, type);

        using var response = await WebhookTestData.PostAsync(
            factory.CreateClient(), payload, WebhookTestData.Sign(payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await WebhookTestData.GetOrderAsync(factory, pending.OrderId);
        var payment = await WebhookTestData.GetPaymentAsync(factory, pending.OrderId);
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Fact]
    public async Task Late_payment_for_a_cancelled_order_is_refunded_automatically()
    {
        var customer = await factory.RegisterAndLoginAsync();
        using var client = factory.CreateAuthorizedClient(customer);
        var pending = await WebhookTestData.CreatePendingPaymentAsync(factory, client);
        using var cancel = await client.PostAsync($"/orders/{pending.OrderId}/cancel", null, Ct);
        cancel.EnsureSuccessStatusCode();
        var payload = WebhookTestData.CheckoutEvent(pending.SessionId);

        using var response = await WebhookTestData.PostAsync(
            factory.CreateClient(), payload, WebhookTestData.Sign(payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await WebhookTestData.GetOrderAsync(factory, pending.OrderId);
        var payment = await WebhookTestData.GetPaymentAsync(factory, pending.OrderId);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(19.98m, payment.RefundedAmount);
        var refund = Assert.Single(factory.Payments.Refunds);
        Assert.Equal($"pi_{pending.SessionId}", refund.PaymentIntentId);
        Assert.Equal(19.98m, refund.Amount);
        Assert.Equal($"late-payment-{payment.Id}", refund.IdempotencyKey);
    }

    [Fact]
    public async Task Late_payment_with_refund_failure_stays_paid_and_returns_200()
    {
        var customer = await factory.RegisterAndLoginAsync();
        using var client = factory.CreateAuthorizedClient(customer);
        var pending = await WebhookTestData.CreatePendingPaymentAsync(factory, client);
        using var cancel = await client.PostAsync($"/orders/{pending.OrderId}/cancel", null, Ct);
        cancel.EnsureSuccessStatusCode();
        var payload = WebhookTestData.CheckoutEvent(pending.SessionId);
        factory.Payments.FailRefund = true;

        using var response = await WebhookTestData.PostAsync(
            factory.CreateClient(), payload, WebhookTestData.Sign(payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await WebhookTestData.GetOrderAsync(factory, pending.OrderId);
        var payment = await WebhookTestData.GetPaymentAsync(factory, pending.OrderId);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Equal(0m, payment.RefundedAmount);
        Assert.Empty(factory.Payments.Refunds);
    }
}