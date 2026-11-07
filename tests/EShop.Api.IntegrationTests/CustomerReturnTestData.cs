using System.Net.Http.Json;
using EShop.Api.Catalog;
using EShop.Api.Orders;
using EShop.Api.Returns;
using EShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EShop.Api.IntegrationTests;

internal sealed record PlacedOrder(Guid OrderId, AdminProductResponse Product, int Quantity);

internal static class CustomerReturnTestData
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<PlacedOrder> PlaceOrderAsync(HttpClient admin, HttpClient customer, int quantity)
    {
        var product = await CheckoutTestData.CreateProductWithStockAsync(admin, quantity + 5);
        await CheckoutTestData.AddToCartAsync(customer, product.Id, quantity);

        using var response = await customer.PostAsJsonAsync("/orders", CheckoutTestData.CourierRequest(), Ct);
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>(Ct);
        return new PlacedOrder(order!.Id, product, quantity);
    }

    public static async Task<PlacedOrder> PlaceDeliveredOrderAsync(HttpClient admin, HttpClient customer, int quantity)
    {
        var placed = await PlaceOrderAsync(admin, customer, quantity);
        foreach (var action in new[] { "mark-paid", "ship", "deliver" })
        {
            using var response = await admin.PostAsync($"/admin/orders/{placed.OrderId}/{action}", null, Ct);
            response.EnsureSuccessStatusCode();
        }

        return placed;
    }

    public static async Task SetDeliveredAtAsync(ApiFactory factory, Guid orderId, DateTimeOffset value)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Orders
            .Where(o => o.Id == orderId)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.DeliveredAt, value), Ct);
    }

    public static CreateReturnBody Body(Guid productId, int quantity, string reason = "Damaged") =>
        new(reason, [new ReturnLineBody(productId, quantity)]);

    public static Task<HttpResponseMessage> PostReturnAsync(HttpClient client, Guid orderId, CreateReturnBody body) =>
        client.PostAsJsonAsync($"/orders/{orderId}/returns", body, Ct);

    public static async Task<ReturnResponse> CreateReturnAsync(
        HttpClient client, Guid orderId, Guid productId, int quantity)
    {
        using var response = await PostReturnAsync(client, orderId, Body(productId, quantity));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReturnResponse>(Ct))!;
    }
}