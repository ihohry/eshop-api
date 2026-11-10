using System.Net;
using System.Net.Http.Json;
using EShop.Api.Carts;
using EShop.Domain.Carts;

using Xunit;

namespace EShop.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class ConcurrentCartTests(ApiFactory factory) : IAsyncLifetime
{
    private const int RequestCount = 5;
    private const string ItemsUrl = "/cart/items";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task First_cart_race_with_different_products_keeps_all_items()
    {
        using var admin = factory.CreateAuthorizedClient(await factory.RegisterAdminAndLoginAsync());
        var ids = new List<Guid>();
        for (var i = 0; i < RequestCount; i++)
        {
            ids.Add((await CheckoutTestData.CreateProductWithStockAsync(admin, 10)).Id);
        }

        using var client = factory.CreateAuthorizedClient(await factory.RegisterAndLoginAsync());
        var next = -1;

        var statuses = await ConcurrencyTestData.SendAllAsync(
            Enumerable.Repeat(client, RequestCount).ToList(),
            c => c.PostAsJsonAsync(ItemsUrl, new AddCartItemRequest(ids[Interlocked.Increment(ref next)], 1), Ct));

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
        var cart = await ReadCartAsync(client);
        Assert.Equal(RequestCount, cart.Items.Count);
        Assert.Equal(RequestCount, cart.TotalQuantity);
    }

    [Fact]
    public async Task First_cart_race_with_the_same_product_creates_one_line()
    {
        using var admin = factory.CreateAuthorizedClient(await factory.RegisterAdminAndLoginAsync());
        var product = await CheckoutTestData.CreateProductWithStockAsync(admin, 10);
        using var client = factory.CreateAuthorizedClient(await factory.RegisterAndLoginAsync());

        var statuses = await ConcurrencyTestData.SendAllAsync(
            Enumerable.Repeat(client, RequestCount).ToList(),
            c => c.PostAsJsonAsync(ItemsUrl, new AddCartItemRequest(product.Id, 1), Ct));

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
        var cart = await ReadCartAsync(client);
        Assert.Equal(RequestCount, Assert.Single(cart.Items).Quantity);
    }

    [Fact]
    public async Task Parallel_adds_to_an_existing_line_add_up()
    {
        using var admin = factory.CreateAuthorizedClient(await factory.RegisterAdminAndLoginAsync());
        var product = await CheckoutTestData.CreateProductWithStockAsync(admin, 10);
        using var client = factory.CreateAuthorizedClient(await factory.RegisterAndLoginAsync());
        await CheckoutTestData.AddToCartAsync(client, product.Id, 1);

        var statuses = await ConcurrencyTestData.SendAllAsync(
            Enumerable.Repeat(client, RequestCount).ToList(),
            c => c.PostAsJsonAsync(ItemsUrl, new AddCartItemRequest(product.Id, 1), Ct));

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
        var cart = await ReadCartAsync(client);
        Assert.Equal(RequestCount + 1, Assert.Single(cart.Items).Quantity);
    }

    private static async Task<CartView> ReadCartAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/cart", Ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CartView>(Ct)
            ?? throw new InvalidOperationException("The cart response was empty.");
    }
}