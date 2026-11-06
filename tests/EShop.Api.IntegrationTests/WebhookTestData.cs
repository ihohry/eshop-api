using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EShop.Api.Orders;
using EShop.Domain.Orders;
using EShop.Domain.Payments;
using EShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EShop.Api.IntegrationTests;

internal sealed record PendingPayment(Guid OrderId, string SessionId);

internal static class WebhookTestData
{
    private const string Secret = "whsec_dummy";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<PendingPayment> CreatePendingPaymentAsync(ApiFactory factory, HttpClient customer)
    {
        var orderId = await PaymentTestData.CreateOrderAsync(factory, customer);
        using var response = await customer.PostAsync($"/orders/{orderId}/pay", null, Ct);
        response.EnsureSuccessStatusCode();
        var link = await response.Content.ReadFromJsonAsync<PaymentLinkResponse>(Ct);
        return new PendingPayment(orderId, new Uri(link!.Url).Segments[^1]);
    }

    public static string CheckoutEvent(
        string sessionId,
        string paymentStatus = "paid",
        string type = "checkout.session.completed")
    {
        var stripeEvent = new
        {
            id = $"evt_{Guid.NewGuid():N}",
            @object = "event",
            api_version = "2025-01-01",
            created = 1700000000,
            type,
            data = new
            {
                @object = new
                {
                    id = sessionId,
                    @object = "checkout.session",
                    payment_status = paymentStatus,
                    payment_intent = $"pi_{sessionId}",
                },
            },
        };
        return JsonSerializer.Serialize(stripeEvent);
    }

    public static string Sign(string payload)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(Secret),
            Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return $"t={timestamp},v1={Convert.ToHexStringLower(hash)}";
    }

    public static async Task<HttpResponseMessage> PostAsync(
        HttpClient client, string payload, string? signature = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        if (signature is not null)
        {
            request.Headers.Add("Stripe-Signature", signature);
        }

        return await client.SendAsync(request, Ct);
    }

    public static async Task<Order> GetOrderAsync(ApiFactory factory, Guid orderId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId, Ct);
    }

    public static async Task<Payment> GetPaymentAsync(ApiFactory factory, Guid orderId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Payments.AsNoTracking().SingleAsync(p => p.OrderId == orderId, Ct);
    }
}