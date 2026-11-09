using System.Net.Http.Json;
using EShop.Api.Carts;
using EShop.Api.Catalog;
using EShop.Api.Orders;
using EShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EShop.Api.IntegrationTests;

internal static class CheckoutTestData
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static CheckoutRequest CourierRequest() => new(
        "Courier",
        new ShippingAddressRequest("Test Customer", "+31612345678", "Main Street 1", null, "1011 AA", "Test City", "NL"),
        null);

    public static async Task<AdminProductResponse> CreateProductWithStockAsync(HttpClient admin, int stock)
    {
        var product = await ImageTestData.CreateProductAsync(admin);
        using var response = await admin.PutAsJsonAsync(
            $"/admin/products/{product.Id}/stock", new StockUpdateRequest(stock), Ct);
        response.EnsureSuccessStatusCode();
        return product with { StockQuantity = stock };
    }

    public static async Task AddToCartAsync(HttpClient client, Guid productId, int quantity)
    {
        using var response = await client.PostAsJsonAsync(
            "/cart/items", new AddCartItemRequest(productId, quantity), Ct);
        response.EnsureSuccessStatusCode();
    }

    public static async Task<int> StockOfAsync(ApiFactory factory, Guid productId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Products
            .AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => p.StockQuantity)
            .SingleAsync(Ct);
    }

    public static async Task<int> CartItemCountAsync(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Carts.SelectMany(c => c.Items).CountAsync(Ct);
    }

    public static async Task<int> OrderCountAsync(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Orders.CountAsync(Ct);
    }

    public static CheckoutRequest PickupRequest(string paymentMethod) => new(
        "PickupPoint",
        new ShippingAddressRequest("Test Customer", "+31612345678", "Pickup Street 5", null, "1011 AB", "Test City", "NL"),
        new PickupPointRequest("PP-1", "Test Pickup Point"),
        paymentMethod);
}