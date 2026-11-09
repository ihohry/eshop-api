using System.Net.Http.Json;
using EShop.Api.Orders;

namespace EShop.Api.IntegrationTests;

internal static class PayOnReceiptTestData
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<DeliveredOrder> CreateDeliveredOrderAsync(ApiFactory factory, HttpClient admin)
    {
        var user = await factory.RegisterAndLoginAsync();
        var customer = factory.CreateAuthorizedClient(user);
        var product = await CheckoutTestData.CreateProductWithStockAsync(admin, 5);
        await CheckoutTestData.AddToCartAsync(customer, product.Id, 2);

        using var checkout = await customer.PostAsJsonAsync(
            "/orders", CheckoutTestData.PickupRequest("PayOnReceipt"), Ct);
        checkout.EnsureSuccessStatusCode();
        var order = await checkout.Content.ReadFromJsonAsync<OrderResponse>(Ct);
        var orderId = order!.Id;

        foreach (var action in new[] { "confirm", "ship", "deliver" })
        {
            using var step = await admin.PostAsync($"/admin/orders/{orderId}/{action}", null, Ct);
            step.EnsureSuccessStatusCode();
        }

        return new DeliveredOrder(orderId, product.Id, customer);
    }
}