using System.Net;
using System.Net.Http.Json;
using EShop.Api.Returns;

namespace EShop.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class CustomerReturnTests(ApiFactory factory) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<(HttpClient Admin, HttpClient Customer)> CreateClientsAsync()
    {
        var adminUser = await factory.RegisterAdminAndLoginAsync();
        var customerUser = await factory.RegisterAndLoginAsync();
        return (factory.CreateAuthorizedClient(adminUser), factory.CreateAuthorizedClient(customerUser));
    }

    [Fact]
    public async Task Create_ReturnsRequestedReturnWithRefundAmount()
    {
        var (admin, customer) = await CreateClientsAsync();
        using var _a = admin;
        using var _c = customer;
        var placed = await CustomerReturnTestData.PlaceDeliveredOrderAsync(admin, customer, 3);

        using var response = await CustomerReturnTestData.PostReturnAsync(
            customer, placed.OrderId, CustomerReturnTestData.Body(placed.Product.Id, 2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ReturnResponse>(Ct);
        Assert.Equal("Requested", body!.Status);
        Assert.Equal(placed.OrderId, body.OrderId);
        Assert.Equal(placed.Product.Price * 2, body.RefundAmount);
        Assert.Equal(2, Assert.Single(body.Items).Quantity);
    }

    [Fact]
    public async Task List_ReturnsOnlyReturnsOfTheOrder()
    {
        var (admin, customer) = await CreateClientsAsync();
        using var _a = admin;
        using var _c = customer;
        var placed = await CustomerReturnTestData.PlaceDeliveredOrderAsync(admin, customer, 3);
        var created = await CustomerReturnTestData.CreateReturnAsync(customer, placed.OrderId, placed.Product.Id, 1);

        var list = await customer.GetFromJsonAsync<List<ReturnResponse>>($"/orders/{placed.OrderId}/returns", Ct);

        Assert.Equal(created.Id, Assert.Single(list!).Id);
    }

    [Fact]
    public async Task Anonymous_Gives401()
    {
        using var anonymous = factory.CreateClient();

        using var response = await anonymous.GetAsync($"/orders/{Guid.NewGuid()}/returns", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnotherUsersOrder_Gives404ForCreateAndList()
    {
        var (admin, customer) = await CreateClientsAsync();
        using var _a = admin;
        using var _c = customer;
        var otherUser = await factory.RegisterAndLoginAsync();
        using var other = factory.CreateAuthorizedClient(otherUser);
        var placed = await CustomerReturnTestData.PlaceDeliveredOrderAsync(admin, customer, 3);

        using var create = await CustomerReturnTestData.PostReturnAsync(
            other, placed.OrderId, CustomerReturnTestData.Body(placed.Product.Id, 1));
        using var list = await other.GetAsync($"/orders/{placed.OrderId}/returns", Ct);

        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
    }

    [Fact]
    public async Task OrderNotDelivered_Gives409()
    {
        var (admin, customer) = await CreateClientsAsync();
        using var _a = admin;
        using var _c = customer;
        var placed = await CustomerReturnTestData.PlaceOrderAsync(admin, customer, 3);

        using var response = await CustomerReturnTestData.PostReturnAsync(
            customer, placed.OrderId, CustomerReturnTestData.Body(placed.Product.Id, 1));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ReturnWindowClosed_Gives409()
    {
        var (admin, customer) = await CreateClientsAsync();
        using var _a = admin;
        using var _c = customer;
        var placed = await CustomerReturnTestData.PlaceDeliveredOrderAsync(admin, customer, 3);
        await CustomerReturnTestData.SetDeliveredAtAsync(factory, placed.OrderId, DateTimeOffset.UtcNow.AddDays(-15));

        using var response = await CustomerReturnTestData.PostReturnAsync(
            customer, placed.OrderId, CustomerReturnTestData.Body(placed.Product.Id, 1));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UnknownProduct_Gives422()
    {
        var (admin, customer) = await CreateClientsAsync();
        using var _a = admin;
        using var _c = customer;
        var placed = await CustomerReturnTestData.PlaceDeliveredOrderAsync(admin, customer, 3);

        using var response = await CustomerReturnTestData.PostReturnAsync(
            customer, placed.OrderId, CustomerReturnTestData.Body(Guid.NewGuid(), 1));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task DuplicateLine_Gives422()
    {
        var (admin, customer) = await CreateClientsAsync();
        using var _a = admin;
        using var _c = customer;
        var placed = await CustomerReturnTestData.PlaceDeliveredOrderAsync(admin, customer, 3);
        var body = new CreateReturnBody("Damaged",
            [new ReturnLineBody(placed.Product.Id, 1), new ReturnLineBody(placed.Product.Id, 1)]);

        using var response = await CustomerReturnTestData.PostReturnAsync(customer, placed.OrderId, body);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task QuantityAboveBought_Gives422()
    {
        var (admin, customer) = await CreateClientsAsync();
        using var _a = admin;
        using var _c = customer;
        var placed = await CustomerReturnTestData.PlaceDeliveredOrderAsync(admin, customer, 3);

        using var response = await CustomerReturnTestData.PostReturnAsync(
            customer, placed.OrderId, CustomerReturnTestData.Body(placed.Product.Id, 4));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task QuantitiesAddUpAcrossSeveralReturns()
    {
        var (admin, customer) = await CreateClientsAsync();
        using var _a = admin;
        using var _c = customer;
        var placed = await CustomerReturnTestData.PlaceDeliveredOrderAsync(admin, customer, 3);
        await CustomerReturnTestData.CreateReturnAsync(customer, placed.OrderId, placed.Product.Id, 2);

        using var tooMany = await CustomerReturnTestData.PostReturnAsync(
            customer, placed.OrderId, CustomerReturnTestData.Body(placed.Product.Id, 2));
        using var enough = await CustomerReturnTestData.PostReturnAsync(
            customer, placed.OrderId, CustomerReturnTestData.Body(placed.Product.Id, 1));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooMany.StatusCode);
        Assert.Equal(HttpStatusCode.Created, enough.StatusCode);
    }

    [Fact]
    public async Task RejectedReturn_DoesNotCountAgainstTheQuantity()
    {
        var (admin, customer) = await CreateClientsAsync();
        using var _a = admin;
        using var _c = customer;
        var placed = await CustomerReturnTestData.PlaceDeliveredOrderAsync(admin, customer, 3);
        var first = await CustomerReturnTestData.CreateReturnAsync(customer, placed.OrderId, placed.Product.Id, 3);
        using var reject = await admin.PostAsync($"/admin/returns/{first.Id}/reject", null, Ct);
        reject.EnsureSuccessStatusCode();

        using var again = await CustomerReturnTestData.PostReturnAsync(
            customer, placed.OrderId, CustomerReturnTestData.Body(placed.Product.Id, 3));

        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("Damaged", 0)]
    [InlineData("Damaged", 10001)]
    public async Task InvalidBody_Gives400(string reason, int quantity)
    {
        var (admin, customer) = await CreateClientsAsync();
        using var _a = admin;
        using var _c = customer;
        var placed = await CustomerReturnTestData.PlaceDeliveredOrderAsync(admin, customer, 3);

        using var response = await CustomerReturnTestData.PostReturnAsync(
            customer, placed.OrderId, CustomerReturnTestData.Body(placed.Product.Id, quantity, reason));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ParallelRequests_NeverReturnMoreThanBought()
    {
        var (admin, customer) = await CreateClientsAsync();
        using var _a = admin;
        using var _c = customer;
        var placed = await CustomerReturnTestData.PlaceDeliveredOrderAsync(admin, customer, 3);
        var clients = Enumerable.Repeat(customer, 5).ToList();

        var results = await ConcurrencyTestData.SendAllAsync(clients, client =>
            CustomerReturnTestData.PostReturnAsync(client, placed.OrderId, CustomerReturnTestData.Body(placed.Product.Id, 1)));

        Assert.Equal(3, results.Count(s => s == HttpStatusCode.Created));
        Assert.Equal(2, results.Count(s => s == HttpStatusCode.UnprocessableEntity));
    }
}