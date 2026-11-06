using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EShop.Api.Orders;
using Xunit;

namespace EShop.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class CheckoutTests(ApiFactory factory) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Checkout_creates_order_reduces_stock_and_clears_cart()
    {
        var adminUser = await factory.RegisterAdminAndLoginAsync();
        using var admin = factory.CreateAuthorizedClient(adminUser);
        var product = await CheckoutTestData.CreateProductWithStockAsync(admin, 5);

        var customer = await factory.RegisterAndLoginAsync();
        using var client = factory.CreateAuthorizedClient(customer);
        await CheckoutTestData.AddToCartAsync(client, product.Id, 2);

        using var response = await client.PostAsJsonAsync("/orders", CheckoutTestData.CourierRequest(), Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>(Ct);

        Assert.NotNull(order);
        Assert.Equal("PendingPayment", order.Status);
        Assert.Equal(19.98m, order.Total);
        var item = Assert.Single(order.Items);
        Assert.Equal(product.Id, item.ProductId);
        Assert.Equal(9.99m, item.UnitPrice);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(3, await CheckoutTestData.StockOfAsync(factory, product.Id));
        Assert.Equal(0, await CheckoutTestData.CartItemCountAsync(factory));
    }

    [Fact]
    public async Task Checkout_with_empty_cart_returns_422()
    {
        var customer = await factory.RegisterAndLoginAsync();
        using var client = factory.CreateAuthorizedClient(customer);

        using var response = await client.PostAsJsonAsync("/orders", CheckoutTestData.CourierRequest(), Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(0, await CheckoutTestData.OrderCountAsync(factory));
    }

    [Fact]
    public async Task Checkout_with_too_little_stock_returns_409_and_changes_nothing()
    {
        var adminUser = await factory.RegisterAdminAndLoginAsync();
        using var admin = factory.CreateAuthorizedClient(adminUser);
        var product = await CheckoutTestData.CreateProductWithStockAsync(admin, 1);

        var customer = await factory.RegisterAndLoginAsync();
        using var client = factory.CreateAuthorizedClient(customer);
        await CheckoutTestData.AddToCartAsync(client, product.Id, 2);

        using var response = await client.PostAsJsonAsync("/orders", CheckoutTestData.CourierRequest(), Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var lines = await ReadLinesAsync(response);
        var line = Assert.Single(lines);
        Assert.Equal(product.Id, line.ProductId);
        Assert.Equal(1, await CheckoutTestData.StockOfAsync(factory, product.Id));
        Assert.Equal(1, await CheckoutTestData.CartItemCountAsync(factory));
        Assert.Equal(0, await CheckoutTestData.OrderCountAsync(factory));
    }

    [Fact]
    public async Task Checkout_with_inactive_product_returns_409_and_changes_nothing()
    {
        var adminUser = await factory.RegisterAdminAndLoginAsync();
        using var admin = factory.CreateAuthorizedClient(adminUser);
        var product = await CheckoutTestData.CreateProductWithStockAsync(admin, 5);

        var customer = await factory.RegisterAndLoginAsync();
        using var client = factory.CreateAuthorizedClient(customer);
        await CheckoutTestData.AddToCartAsync(client, product.Id, 1);

        using var deactivate = await admin.DeleteAsync($"/admin/products/{product.Id}", Ct);
        deactivate.EnsureSuccessStatusCode();

        using var response = await client.PostAsJsonAsync("/orders", CheckoutTestData.CourierRequest(), Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var lines = await ReadLinesAsync(response);
        Assert.Equal(product.Id, Assert.Single(lines).ProductId);
        Assert.Equal(5, await CheckoutTestData.StockOfAsync(factory, product.Id));
        Assert.Equal(1, await CheckoutTestData.CartItemCountAsync(factory));
        Assert.Equal(0, await CheckoutTestData.OrderCountAsync(factory));
    }

    private static async Task<List<CheckoutProblemResponse>> ReadLinesAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        using var document = JsonDocument.Parse(body);
        var raw = document.RootElement.GetProperty("lines").GetRawText();
        return JsonSerializer.Deserialize<List<CheckoutProblemResponse>>(raw, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("The lines list was empty.");
    }
}