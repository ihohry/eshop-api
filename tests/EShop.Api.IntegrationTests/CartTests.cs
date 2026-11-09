using System.Net;
using System.Net.Http.Json;
using EShop.Api.Carts;
using EShop.Domain.Carts;
using Xunit;

namespace EShop.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class CartTests(ApiFactory factory) : IAsyncLifetime
{
    private const string ItemsUrl = "/cart/items";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Cart_requires_authentication()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/cart", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Empty_cart_is_returned_for_a_new_user()
    {
        using var client = await NewCustomerAsync();

        using var response = await client.GetAsync("/cart", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cart = await ReadCartAsync(response);
        Assert.Empty(cart.Items);
        Assert.Equal(0, cart.TotalQuantity);
        Assert.Equal(0m, cart.Subtotal);
        Assert.Equal("EUR", cart.Currency);
    }

    [Fact]
    public async Task Adding_a_product_returns_the_updated_cart()
    {
        using var admin = await NewAdminAsync();
        var product = await CheckoutTestData.CreateProductWithStockAsync(admin, 5);
        using var client = await NewCustomerAsync();

        using var response = await client.PostAsJsonAsync(ItemsUrl, new AddCartItemRequest(product.Id, 2), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cart = await ReadCartAsync(response);
        var item = Assert.Single(cart.Items);
        Assert.Equal(product.Id, item.ProductId);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(9.99m, item.UnitPrice);
        Assert.Equal(19.98m, item.LineTotal);
        Assert.True(item.IsAvailable);
        Assert.Equal(2, cart.TotalQuantity);
        Assert.Equal(19.98m, cart.Subtotal);
    }

    [Fact]
    public async Task Adding_the_same_product_twice_merges_the_quantities()
    {
        using var admin = await NewAdminAsync();
        var product = await CheckoutTestData.CreateProductWithStockAsync(admin, 10);
        using var client = await NewCustomerAsync();
        await CheckoutTestData.AddToCartAsync(client, product.Id, 2);

        using var response = await client.PostAsJsonAsync(ItemsUrl, new AddCartItemRequest(product.Id, 3), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cart = await ReadCartAsync(response);
        Assert.Equal(5, Assert.Single(cart.Items).Quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task Quantity_outside_the_range_returns_400(int quantity)
    {
        using var admin = await NewAdminAsync();
        var product = await CheckoutTestData.CreateProductWithStockAsync(admin, 5);
        using var client = await NewCustomerAsync();

        using var response = await client.PostAsJsonAsync(ItemsUrl, new AddCartItemRequest(product.Id, quantity), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CheckoutTestData.CartItemCountAsync(factory));
    }

    [Fact]
    public async Task Adding_more_than_99_of_one_product_returns_422()
    {
        using var admin = await NewAdminAsync();
        var product = await CheckoutTestData.CreateProductWithStockAsync(admin, 1);
        using var client = await NewCustomerAsync();
        await CheckoutTestData.AddToCartAsync(client, product.Id, 99);

        using var response = await client.PostAsJsonAsync(ItemsUrl, new AddCartItemRequest(product.Id, 1), Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var cartResponse = await client.GetAsync("/cart", Ct);
        Assert.Equal(99, Assert.Single((await ReadCartAsync(cartResponse)).Items).Quantity);
    }

    [Fact]
    public async Task A_cart_holds_at_most_99_different_products()
    {
        using var admin = await NewAdminAsync();
        using var client = await NewCustomerAsync();
        Guid firstId = Guid.Empty;
        for (var i = 0; i < CartDefaults.MaxDistinctItems; i++)
        {
            var product = await ImageTestData.CreateProductAsync(admin);
            if (i == 0)
            {
                firstId = product.Id;
            }

            await CheckoutTestData.AddToCartAsync(client, product.Id, 1);
        }

        var extra = await ImageTestData.CreateProductAsync(admin);
        using var rejected = await client.PostAsJsonAsync(ItemsUrl, new AddCartItemRequest(extra.Id, 1), Ct);
        using var existing = await client.PostAsJsonAsync(ItemsUrl, new AddCartItemRequest(firstId, 1), Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.OK, existing.StatusCode);
        Assert.Equal(CartDefaults.MaxDistinctItems, (await ReadCartAsync(existing)).Items.Count);
    }

    [Fact]
    public async Task Adding_an_unknown_product_returns_422()
    {
        using var client = await NewCustomerAsync();

        using var response = await client.PostAsJsonAsync(ItemsUrl, new AddCartItemRequest(Guid.NewGuid(), 1), Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(0, await CheckoutTestData.CartItemCountAsync(factory));
    }

    [Fact]
    public async Task Adding_an_inactive_product_returns_422()
    {
        using var admin = await NewAdminAsync();
        var product = await CheckoutTestData.CreateProductWithStockAsync(admin, 5);
        using var deactivate = await admin.DeleteAsync($"/admin/products/{product.Id}", Ct);
        deactivate.EnsureSuccessStatusCode();
        using var client = await NewCustomerAsync();

        using var response = await client.PostAsJsonAsync(ItemsUrl, new AddCartItemRequest(product.Id, 1), Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(0, await CheckoutTestData.CartItemCountAsync(factory));
    }

    [Fact]
    public async Task Setting_the_quantity_replaces_it()
    {
        using var admin = await NewAdminAsync();
        var product = await CheckoutTestData.CreateProductWithStockAsync(admin, 10);
        using var client = await NewCustomerAsync();
        await CheckoutTestData.AddToCartAsync(client, product.Id, 5);

        using var response = await client.PutAsJsonAsync(
            $"{ItemsUrl}/{product.Id}", new SetCartItemQuantityRequest(2), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cart = await ReadCartAsync(response);
        Assert.Equal(2, Assert.Single(cart.Items).Quantity);
        Assert.Equal(19.98m, cart.Subtotal);
    }

    [Fact]
    public async Task Setting_the_quantity_of_a_product_not_in_the_cart_returns_404()
    {
        using var admin = await NewAdminAsync();
        var product = await CheckoutTestData.CreateProductWithStockAsync(admin, 5);
        using var client = await NewCustomerAsync();

        using var response = await client.PutAsJsonAsync(
            $"{ItemsUrl}/{product.Id}", new SetCartItemQuantityRequest(2), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Removing_an_item_is_idempotent()
    {
        using var admin = await NewAdminAsync();
        var kept = await CheckoutTestData.CreateProductWithStockAsync(admin, 5);
        var removed = await CheckoutTestData.CreateProductWithStockAsync(admin, 5);
        using var client = await NewCustomerAsync();
        await CheckoutTestData.AddToCartAsync(client, kept.Id, 1);
        await CheckoutTestData.AddToCartAsync(client, removed.Id, 1);

        using var first = await client.DeleteAsync($"{ItemsUrl}/{removed.Id}", Ct);
        using var second = await client.DeleteAsync($"{ItemsUrl}/{removed.Id}", Ct);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(kept.Id, Assert.Single((await ReadCartAsync(second)).Items).ProductId);
    }

    [Fact]
    public async Task Clearing_the_cart_returns_204_and_can_be_repeated()
    {
        using var admin = await NewAdminAsync();
        var product = await CheckoutTestData.CreateProductWithStockAsync(admin, 5);
        using var client = await NewCustomerAsync();
        await CheckoutTestData.AddToCartAsync(client, product.Id, 3);

        using var first = await client.DeleteAsync("/cart", Ct);
        using var second = await client.DeleteAsync("/cart", Ct);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(0, await CheckoutTestData.CartItemCountAsync(factory));
    }

    [Fact]
    public async Task Line_with_too_little_stock_stays_in_the_cart_but_not_in_the_subtotal()
    {
        using var admin = await NewAdminAsync();
        var enough = await CheckoutTestData.CreateProductWithStockAsync(admin, 5);
        var tooLittle = await CheckoutTestData.CreateProductWithStockAsync(admin, 1);
        using var client = await NewCustomerAsync();
        await CheckoutTestData.AddToCartAsync(client, enough.Id, 2);
        await CheckoutTestData.AddToCartAsync(client, tooLittle.Id, 2);

        using var response = await client.GetAsync("/cart", Ct);

        var cart = await ReadCartAsync(response);
        Assert.Equal(2, cart.Items.Count);
        Assert.True(cart.Items.Single(i => i.ProductId == enough.Id).IsAvailable);
        Assert.False(cart.Items.Single(i => i.ProductId == tooLittle.Id).IsAvailable);
        Assert.Equal(4, cart.TotalQuantity);
        Assert.Equal(19.98m, cart.Subtotal);
    }

    [Fact]
    public async Task Deactivated_product_stays_in_the_cart_as_unavailable()
    {
        using var admin = await NewAdminAsync();
        var product = await CheckoutTestData.CreateProductWithStockAsync(admin, 5);
        using var client = await NewCustomerAsync();
        await CheckoutTestData.AddToCartAsync(client, product.Id, 1);
        using var deactivate = await admin.DeleteAsync($"/admin/products/{product.Id}", Ct);
        deactivate.EnsureSuccessStatusCode();

        using var response = await client.GetAsync("/cart", Ct);

        var cart = await ReadCartAsync(response);
        Assert.False(Assert.Single(cart.Items).IsAvailable);
        Assert.Equal(0m, cart.Subtotal);
    }

    [Fact]
    public async Task Carts_of_different_users_are_separate()
    {
        using var admin = await NewAdminAsync();
        var product = await CheckoutTestData.CreateProductWithStockAsync(admin, 5);
        using var owner = await NewCustomerAsync();
        using var other = await NewCustomerAsync();
        await CheckoutTestData.AddToCartAsync(owner, product.Id, 1);

        using var response = await other.GetAsync("/cart", Ct);

        Assert.Empty((await ReadCartAsync(response)).Items);
    }

    private async Task<HttpClient> NewCustomerAsync() =>
        factory.CreateAuthorizedClient(await factory.RegisterAndLoginAsync());

    private async Task<HttpClient> NewAdminAsync() =>
        factory.CreateAuthorizedClient(await factory.RegisterAdminAndLoginAsync());

    private static async Task<CartView> ReadCartAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<CartView>(Ct)
            ?? throw new InvalidOperationException("The cart response was empty.");
}