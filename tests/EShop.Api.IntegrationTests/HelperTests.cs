using System.Net;

namespace EShop.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class HelperTests(ApiFactory factory) : IAsyncLifetime
{
    public ValueTask InitializeAsync() => new(factory.ResetDatabaseAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Registered_user_can_call_protected_endpoint()
    {
        var user = await factory.RegisterAndLoginAsync();
        using var client = factory.CreateAuthorizedClient(user);

        var response = await client.GetAsync("/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Reset_removes_users_and_sent_emails()
    {
        await factory.RegisterAndLoginAsync();
        Assert.NotEmpty(factory.Email.Sent);

        await factory.ResetDatabaseAsync();

        Assert.Empty(factory.Email.Sent);
    }
}