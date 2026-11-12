using System.Net;
using System.Net.Http.Json;
using EShop.Api.Orders;
using EShop.Api.Returns;
using EShop.Domain.Orders;
using EShop.Domain.Payments;
using Xunit;

namespace EShop.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class FullFlowTests(ApiFactory factory) : IAsyncLifetime
{
    private const int Stock = 5;
    private const int Quantity = 2;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => new(factory.ResetDatabaseAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Customer_buys_receives_and_returns_the_goods_with_a_full_refund()
    {
        var adminUser = await factory.RegisterAdminAndLoginAsync();
        using var admin = factory.CreateAuthorizedClient(adminUser);
        var customerUser = await factory.RegisterAndLoginAsync();
        using var customer = factory.CreateAuthorizedClient(customerUser);
        using var anonymous = factory.CreateClient();

        var product = await CheckoutTestData.CreateProductWithStockAsync(admin, Stock);
        await CheckoutTestData.AddToCartAsync(customer, product.Id, Quantity);

        using var checkout = await customer.PostAsJsonAsync("/orders", CheckoutTestData.CourierRequest(), Ct);
        Assert.Equal(HttpStatusCode.Created, checkout.StatusCode);
        var order = await checkout.Content.ReadFromJsonAsync<OrderResponse>(Ct);
        var orderId = order!.Id;
        Assert.Equal(Stock - Quantity, await CheckoutTestData.StockOfAsync(factory, product.Id));
        Assert.Equal(0, await CheckoutTestData.CartItemCountAsync(factory));
        Assert.Equal(OrderStatus.PendingPayment, (await WebhookTestData.GetOrderAsync(factory, orderId)).Status);

        using var pay = await customer.PostAsync($"/orders/{orderId}/pay", null, Ct);
        Assert.Equal(HttpStatusCode.OK, pay.StatusCode);
        var link = await pay.Content.ReadFromJsonAsync<PaymentLinkResponse>(Ct);
        var sessionId = new Uri(link!.Url).Segments[^1];
        Assert.Single(factory.Payments.CreatedSessions);

        var payload = WebhookTestData.CheckoutEvent(sessionId);
        using var webhook = await WebhookTestData.PostAsync(anonymous, payload, WebhookTestData.Sign(payload));
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);
        Assert.Equal(OrderStatus.Paid, (await WebhookTestData.GetOrderAsync(factory, orderId)).Status);
        Assert.Equal(PaymentStatus.Paid, (await WebhookTestData.GetPaymentAsync(factory, orderId)).Status);

        await PostOkAsync(admin, $"/admin/orders/{orderId}/ship");
        Assert.Equal(OrderStatus.Shipped, (await WebhookTestData.GetOrderAsync(factory, orderId)).Status);
        await PostOkAsync(admin, $"/admin/orders/{orderId}/deliver");
        Assert.Equal(OrderStatus.Delivered, (await WebhookTestData.GetOrderAsync(factory, orderId)).Status);

        var returnRequest = await CustomerReturnTestData.CreateReturnAsync(customer, orderId, product.Id, Quantity);
        await PostOkAsync(admin, $"/admin/returns/{returnRequest.Id}/approve");
        Assert.Equal(Stock - Quantity, await CheckoutTestData.StockOfAsync(factory, product.Id));
        await PostOkAsync(admin, $"/admin/returns/{returnRequest.Id}/receive");
        Assert.Equal(Stock, await CheckoutTestData.StockOfAsync(factory, product.Id));
        await PostOkAsync(admin, $"/admin/returns/{returnRequest.Id}/refund");

        var payment = await WebhookTestData.GetPaymentAsync(factory, orderId);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(payment.Amount, payment.RefundedAmount);
        var refund = Assert.Single(factory.Payments.Refunds);
        Assert.Equal(payment.Amount, refund.Amount);
        Assert.Equal(Stock, await CheckoutTestData.StockOfAsync(factory, product.Id));
    }

    private static async Task PostOkAsync(HttpClient client, string url)
    {
        using var response = await client.PostAsync(url, null, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}