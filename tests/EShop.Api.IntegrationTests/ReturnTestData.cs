using System.Net.Http.Json;
using EShop.Api.Orders;
using EShop.Api.Returns;
using Xunit;

namespace EShop.Api.IntegrationTests;

internal sealed record DeliveredOrder(Guid OrderId, Guid ProductId, HttpClient Customer);

internal static class ReturnTestData
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<DeliveredOrder> CreateDeliveredOrderAsync(ApiFactory factory, HttpClient admin)
    {
        var user = await factory.RegisterAndLoginAsync();
        var customer = factory.CreateAuthorizedClient(user);
        var product = await CheckoutTestData.CreateProductWithStockAsync(admin, 5);
        await CheckoutTestData.AddToCartAsync(customer, product.Id, 2);

        using var checkout = await customer.PostAsJsonAsync("/orders", CheckoutTestData.CourierRequest(), Ct);
        checkout.EnsureSuccessStatusCode();
        var order = await checkout.Content.ReadFromJsonAsync<OrderResponse>(Ct);
        var orderId = order!.Id;

        using var pay = await customer.PostAsync($"/orders/{orderId}/pay", null, Ct);
        pay.EnsureSuccessStatusCode();
        var link = await pay.Content.ReadFromJsonAsync<PaymentLinkResponse>(Ct);
        var sessionId = new Uri(link!.Url).Segments[^1];

        var payload = WebhookTestData.CheckoutEvent(sessionId);
        using var anonymous = factory.CreateClient();
        using var hook = await WebhookTestData.PostAsync(anonymous, payload, WebhookTestData.Sign(payload));
        hook.EnsureSuccessStatusCode();

        foreach (var action in new[] { "ship", "deliver" })
        {
            using var step = await admin.PostAsync($"/admin/orders/{orderId}/{action}", null, Ct);
            step.EnsureSuccessStatusCode();
        }

        return new DeliveredOrder(orderId, product.Id, customer);
    }

    public static async Task<Guid> RequestReturnAsync(DeliveredOrder order, int quantity)
    {
        var body = new CreateReturnBody("Damaged", [new ReturnLineBody(order.ProductId, quantity)]);
        using var response = await order.Customer.PostAsJsonAsync($"/orders/{order.OrderId}/returns", body, Ct);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ReturnResponse>(Ct);
        return created!.Id;
    }

    public static Task<HttpResponseMessage> ActAsync(HttpClient admin, Guid returnId, string action) =>
        admin.PostAsync($"/admin/returns/{returnId}/{action}", null, Ct);

    public static async Task<AdminReturnResponse> GetAsync(HttpClient admin, Guid returnId)
    {
        var response = await admin.GetFromJsonAsync<AdminReturnResponse>($"/admin/returns/{returnId}", Ct);
        return response!;
    }
}