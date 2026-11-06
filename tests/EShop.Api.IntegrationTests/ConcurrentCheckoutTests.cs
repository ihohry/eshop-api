using System.Net;
using System.Net.Http.Json;
using EShop.Api.Orders;
using Xunit;

namespace EShop.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class ConcurrentCheckoutTests(ApiFactory factory) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Ten_customers_buying_the_last_three_items_create_exactly_three_orders()
    {
        var adminUser = await factory.RegisterAdminAndLoginAsync();
        using var admin = factory.CreateAuthorizedClient(adminUser);
        var product = await CheckoutTestData.CreateProductWithStockAsync(admin, 3);

        using var customers = await ConcurrencyTestData.CreateCustomersAsync(factory, 10);
        foreach (var client in customers.Clients)
        {
            await CheckoutTestData.AddToCartAsync(client, product.Id, 1);
        }

        var statuses = await ConcurrencyTestData.SendAllAsync(
            customers.Clients,
            client => client.PostAsJsonAsync("/orders", CheckoutTestData.CourierRequest(), Ct));

        Assert.Equal(3, statuses.Count(s => s == HttpStatusCode.Created));
        Assert.Equal(7, statuses.Count(s => s == HttpStatusCode.Conflict));
        Assert.Equal(0, await CheckoutTestData.StockOfAsync(factory, product.Id));
        Assert.Equal(3, await CheckoutTestData.OrderCountAsync(factory));
        Assert.Equal(7, await CheckoutTestData.CartItemCountAsync(factory));
    }

    [Fact]
    public async Task Carts_with_the_same_products_in_opposite_order_do_not_deadlock()
    {
        var adminUser = await factory.RegisterAdminAndLoginAsync();
        using var admin = factory.CreateAuthorizedClient(adminUser);
        var first = await CheckoutTestData.CreateProductWithStockAsync(admin, 3);
        var second = await CheckoutTestData.CreateProductWithStockAsync(admin, 3);

        using var customers = await ConcurrencyTestData.CreateCustomersAsync(factory, 6);
        for (var i = 0; i < customers.Clients.Count; i++)
        {
            var client = customers.Clients[i];
            var (one, two) = i % 2 == 0 ? (first, second) : (second, first);
            await CheckoutTestData.AddToCartAsync(client, one.Id, 1);
            await CheckoutTestData.AddToCartAsync(client, two.Id, 1);
        }

        var statuses = await ConcurrencyTestData.SendAllAsync(
            customers.Clients,
            client => client.PostAsJsonAsync("/orders", CheckoutTestData.CourierRequest(), Ct));

        Assert.Equal(3, statuses.Count(s => s == HttpStatusCode.Created));
        Assert.Equal(3, statuses.Count(s => s == HttpStatusCode.Conflict));
        Assert.Equal(0, await CheckoutTestData.StockOfAsync(factory, first.Id));
        Assert.Equal(0, await CheckoutTestData.StockOfAsync(factory, second.Id));
        Assert.Equal(3, await CheckoutTestData.OrderCountAsync(factory));
    }

    [Fact]
    public async Task Parallel_cancel_of_one_order_returns_the_stock_only_once()
    {
        var adminUser = await factory.RegisterAdminAndLoginAsync();
        using var admin = factory.CreateAuthorizedClient(adminUser);
        var product = await CheckoutTestData.CreateProductWithStockAsync(admin, 5);

        var customer = await factory.RegisterAndLoginAsync();
        using var client = factory.CreateAuthorizedClient(customer);
        await CheckoutTestData.AddToCartAsync(client, product.Id, 2);

        using var checkout = await client.PostAsJsonAsync("/orders", CheckoutTestData.CourierRequest(), Ct);
        Assert.Equal(HttpStatusCode.Created, checkout.StatusCode);
        var order = await checkout.Content.ReadFromJsonAsync<OrderResponse>(Ct);
        Assert.NotNull(order);
        Assert.Equal(3, await CheckoutTestData.StockOfAsync(factory, product.Id));

        var statuses = await ConcurrencyTestData.SendAllAsync(
            [.. Enumerable.Repeat(client, 5)],
            c => c.PostAsync($"/orders/{order.Id}/cancel", null, Ct));

        Assert.All(statuses, s => Assert.Equal(HttpStatusCode.OK, s));
        Assert.Equal(5, await CheckoutTestData.StockOfAsync(factory, product.Id));

        var current = await client.GetFromJsonAsync<OrderResponse>($"/orders/{order.Id}", Ct);
        Assert.Equal("Cancelled", current!.Status);
    }
}