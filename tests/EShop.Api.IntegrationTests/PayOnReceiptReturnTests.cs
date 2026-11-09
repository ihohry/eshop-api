using System.Net;
using System.Net.Http.Json;
using EShop.Api.Orders;
using Xunit;

namespace EShop.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class PayOnReceiptReturnTests(ApiFactory factory) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Delivered_order_is_marked_paid()
    {
        var (admin, order, _) = await PrepareAsync(approve: false);
        using var _ = admin;

        var loaded = await order.Customer.GetFromJsonAsync<OrderResponse>($"/orders/{order.OrderId}", Ct);

        Assert.Equal("Delivered", loaded!.Status);
        Assert.NotNull(loaded.PaidAt);
    }

    [Fact]
    public async Task Receive_returns_the_stock_of_the_returned_lines()
    {
        var (admin, order, returnId) = await PrepareAsync(approve: true);
        using var _ = admin;
        Assert.Equal(3, await CheckoutTestData.StockOfAsync(factory, order.ProductId));

        using var response = await ReturnTestData.ActAsync(admin, returnId, "receive");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(5, await CheckoutTestData.StockOfAsync(factory, order.ProductId));
    }

    [Fact]
    public async Task Refund_is_only_a_status_change_and_calls_no_gateway()
    {
        var (admin, order, returnId) = await PrepareAsync(approve: true);
        using var _ = admin;
        using var received = await ReturnTestData.ActAsync(admin, returnId, "receive");
        received.EnsureSuccessStatusCode();

        using var response = await ReturnTestData.ActAsync(admin, returnId, "refund");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Refunded", (await ReturnTestData.GetAsync(admin, returnId)).Status);
        Assert.Empty(factory.Payments.Refunds);
        Assert.Equal(5, await CheckoutTestData.StockOfAsync(factory, order.ProductId));
    }

    [Fact]
    public async Task Repeated_refund_returns_200()
    {
        var (admin, _, returnId) = await PrepareAsync(approve: true);
        using var _ = admin;
        using var received = await ReturnTestData.ActAsync(admin, returnId, "receive");
        received.EnsureSuccessStatusCode();

        using var first = await ReturnTestData.ActAsync(admin, returnId, "refund");
        using var second = await ReturnTestData.ActAsync(admin, returnId, "refund");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Empty(factory.Payments.Refunds);
    }

    private async Task<(HttpClient Admin, DeliveredOrder Order, Guid ReturnId)> PrepareAsync(bool approve)
    {
        var adminUser = await factory.RegisterAdminAndLoginAsync();
        var admin = factory.CreateAuthorizedClient(adminUser);
        var order = await PayOnReceiptTestData.CreateDeliveredOrderAsync(factory, admin);
        var returnId = await ReturnTestData.RequestReturnAsync(order, 2);

        if (approve)
        {
            using var response = await ReturnTestData.ActAsync(admin, returnId, "approve");
            response.EnsureSuccessStatusCode();
        }

        return (admin, order, returnId);
    }
}