using EShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using EShop.Api.Orders;

namespace EShop.Api.IntegrationTests;

internal static class PaymentTestData
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<Guid> CreateOrderAsync(ApiFactory factory, HttpClient customer)
    {
        var adminUser = await factory.RegisterAdminAndLoginAsync();
        using var admin = factory.CreateAuthorizedClient(adminUser);
        var product = await CheckoutTestData.CreateProductWithStockAsync(admin, 5);
        await CheckoutTestData.AddToCartAsync(customer, product.Id, 2);

        using var response = await customer.PostAsJsonAsync("/orders", CheckoutTestData.CourierRequest(), Ct);
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>(Ct);
        return order!.Id;
    }

    public static async Task SetOrderExpiryAsync(ApiFactory factory, Guid orderId, DateTimeOffset value)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Orders
            .Where(o => o.Id == orderId)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.ExpiresAt, value), Ct);
    }

    public static async Task SetPaymentExpiryAsync(ApiFactory factory, Guid orderId, DateTimeOffset value)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Payments
            .Where(p => p.OrderId == orderId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ExpiresAt, value), Ct);
    }
}