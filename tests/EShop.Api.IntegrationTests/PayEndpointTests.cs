using System.Net;
using System.Net.Http.Json;
using EShop.Api.Orders;
using Xunit;

namespace EShop.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class PayEndpointTests(ApiFactory factory) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Pay_returns_link_and_creates_one_session()
    {
        var customer = await factory.RegisterAndLoginAsync();
        using var client = factory.CreateAuthorizedClient(customer);
        var orderId = await PaymentTestData.CreateOrderAsync(factory, client);

        using var response = await client.PostAsync($"/orders/{orderId}/pay", null, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var link = await response.Content.ReadFromJsonAsync<PaymentLinkResponse>(Ct);
        Assert.NotNull(link);
        Assert.Equal(orderId, link.OrderId);
        Assert.Equal(19.98m, link.Amount);
        Assert.Equal("EUR", link.Currency);
        Assert.StartsWith("https://test.local/pay/", link.Url, StringComparison.Ordinal);
        Assert.Single(factory.Payments.CreatedSessions);
    }

    [Fact]
    public async Task Pay_twice_returns_the_same_link_without_a_new_session()
    {
        var customer = await factory.RegisterAndLoginAsync();
        using var client = factory.CreateAuthorizedClient(customer);
        var orderId = await PaymentTestData.CreateOrderAsync(factory, client);

        using var first = await client.PostAsync($"/orders/{orderId}/pay", null, Ct);
        using var second = await client.PostAsync($"/orders/{orderId}/pay", null, Ct);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var firstLink = await first.Content.ReadFromJsonAsync<PaymentLinkResponse>(Ct);
        var secondLink = await second.Content.ReadFromJsonAsync<PaymentLinkResponse>(Ct);
        Assert.Equal(firstLink!.Url, secondLink!.Url);
        Assert.Single(factory.Payments.CreatedSessions);
    }

    [Fact]
    public async Task Pay_after_the_link_expired_creates_a_new_session()
    {
        var customer = await factory.RegisterAndLoginAsync();
        using var client = factory.CreateAuthorizedClient(customer);
        var orderId = await PaymentTestData.CreateOrderAsync(factory, client);

        using var first = await client.PostAsync($"/orders/{orderId}/pay", null, Ct);
        await PaymentTestData.SetPaymentExpiryAsync(factory, orderId, factory.Time.GetUtcNow().AddMinutes(-1));
        using var second = await client.PostAsync($"/orders/{orderId}/pay", null, Ct);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var firstLink = await first.Content.ReadFromJsonAsync<PaymentLinkResponse>(Ct);
        var secondLink = await second.Content.ReadFromJsonAsync<PaymentLinkResponse>(Ct);
        Assert.NotEqual(firstLink!.Url, secondLink!.Url);
        Assert.Equal(2, factory.Payments.CreatedSessions.Count);
    }

    [Fact]
    public async Task Pay_with_less_than_31_minutes_left_returns_409_and_creates_no_session()
    {
        var customer = await factory.RegisterAndLoginAsync();
        using var client = factory.CreateAuthorizedClient(customer);
        var orderId = await PaymentTestData.CreateOrderAsync(factory, client);
        await PaymentTestData.SetOrderExpiryAsync(factory, orderId, factory.Time.GetUtcNow().AddMinutes(30));

        using var response = await client.PostAsync($"/orders/{orderId}/pay", null, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(factory.Payments.CreatedSessions);
    }

    [Fact]
    public async Task Pay_for_another_users_order_returns_404()
    {
        var owner = await factory.RegisterAndLoginAsync();
        using var ownerClient = factory.CreateAuthorizedClient(owner);
        var orderId = await PaymentTestData.CreateOrderAsync(factory, ownerClient);

        var other = await factory.RegisterAndLoginAsync();
        using var otherClient = factory.CreateAuthorizedClient(other);

        using var response = await otherClient.PostAsync($"/orders/{orderId}/pay", null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(factory.Payments.CreatedSessions);
    }

    [Fact]
    public async Task Pay_for_a_cancelled_order_returns_409()
    {
        var customer = await factory.RegisterAndLoginAsync();
        using var client = factory.CreateAuthorizedClient(customer);
        var orderId = await PaymentTestData.CreateOrderAsync(factory, client);
        using var cancel = await client.PostAsync($"/orders/{orderId}/cancel", null, Ct);
        cancel.EnsureSuccessStatusCode();

        using var response = await client.PostAsync($"/orders/{orderId}/pay", null, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(factory.Payments.CreatedSessions);
    }

    [Fact]
    public async Task Pay_with_provider_failure_returns_502_and_a_retry_works()
    {
        var customer = await factory.RegisterAndLoginAsync();
        using var client = factory.CreateAuthorizedClient(customer);
        var orderId = await PaymentTestData.CreateOrderAsync(factory, client);

        factory.Payments.FailCreate = true;
        using var failed = await client.PostAsync($"/orders/{orderId}/pay", null, Ct);
        factory.Payments.FailCreate = false;
        using var retry = await client.PostAsync($"/orders/{orderId}/pay", null, Ct);

        Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Single(factory.Payments.CreatedSessions);
    }

    [Fact]
    public async Task Pay_without_login_returns_401()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsync($"/orders/{Guid.NewGuid()}/pay", null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}