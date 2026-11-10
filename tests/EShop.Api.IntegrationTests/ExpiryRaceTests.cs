using System.Net;
using System.Net.Http.Json;
using EShop.Api.Orders;
using EShop.Infrastructure.Orders;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EShop.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class ExpiryRaceTests(ApiFactory factory) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly HttpStatusCode[] CancelOutcomes = [HttpStatusCode.OK, HttpStatusCode.Conflict];
    private static readonly HttpStatusCode[] CheckoutOutcomes = [HttpStatusCode.Created, HttpStatusCode.Conflict];
    private static readonly string[] EndStatuses = ["Cancelled", "Expired"];

    public async ValueTask InitializeAsync() => await factory.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Expiry_and_cancel_of_the_same_order_return_the_stock_only_once()
    {
        var adminUser = await factory.RegisterAdminAndLoginAsync();
        using var admin = factory.CreateAuthorizedClient(adminUser);
        var product = await CheckoutTestData.CreateProductWithStockAsync(admin, 5);

        var customer = await factory.RegisterAndLoginAsync();
        using var client = factory.CreateAuthorizedClient(customer);
        var orderId = await PlaceOrderAsync(client, product.Id, 2);
        Assert.Equal(3, await CheckoutTestData.StockOfAsync(factory, product.Id));
        await PaymentTestData.SetOrderExpiryAsync(factory, orderId, DateTimeOffset.UtcNow.AddMinutes(-1));

        var cancel = client.PostAsync($"/orders/{orderId}/cancel", null, Ct);
        var expiry = RunExpiryAsync();
        await Task.WhenAll(cancel, expiry);

        using var response = await cancel;
        Assert.Contains(response.StatusCode, CancelOutcomes);
        Assert.Equal(5, await CheckoutTestData.StockOfAsync(factory, product.Id));

        var current = await client.GetFromJsonAsync<OrderResponse>($"/orders/{orderId}", Ct);
        Assert.Contains(current!.Status, EndStatuses);
    }

    [Fact]
    public async Task Checkout_during_expiry_never_oversells_the_released_stock()
    {
        var adminUser = await factory.RegisterAdminAndLoginAsync();
        using var admin = factory.CreateAuthorizedClient(adminUser);
        var product = await CheckoutTestData.CreateProductWithStockAsync(admin, 1);

        var first = await factory.RegisterAndLoginAsync();
        using var firstClient = factory.CreateAuthorizedClient(first);
        var expiredOrderId = await PlaceOrderAsync(firstClient, product.Id, 1);
        Assert.Equal(0, await CheckoutTestData.StockOfAsync(factory, product.Id));
        await PaymentTestData.SetOrderExpiryAsync(factory, expiredOrderId, DateTimeOffset.UtcNow.AddMinutes(-1));

        var second = await factory.RegisterAndLoginAsync();
        using var secondClient = factory.CreateAuthorizedClient(second);
        await CheckoutTestData.AddToCartAsync(secondClient, product.Id, 1);

        var checkout = secondClient.PostAsJsonAsync("/orders", CheckoutTestData.CourierRequest(), Ct);
        var expiry = RunExpiryAsync();
        await Task.WhenAll(checkout, expiry);

        using var response = await checkout;
        Assert.Contains(response.StatusCode, CheckoutOutcomes);
        var expectedStock = response.StatusCode == HttpStatusCode.Created ? 0 : 1;
        Assert.Equal(expectedStock, await CheckoutTestData.StockOfAsync(factory, product.Id));

        var expired = await firstClient.GetFromJsonAsync<OrderResponse>($"/orders/{expiredOrderId}", Ct);
        Assert.Equal("Expired", expired!.Status);
    }

    private static async Task<Guid> PlaceOrderAsync(HttpClient client, Guid productId, int quantity)
    {
        await CheckoutTestData.AddToCartAsync(client, productId, quantity);
        using var response = await client.PostAsJsonAsync("/orders", CheckoutTestData.CourierRequest(), Ct);
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>(Ct);
        return order!.Id;
    }

    private async Task RunExpiryAsync()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<OrderExpiryService>();
        await service.ExpireDueAsync(Ct);
    }
}