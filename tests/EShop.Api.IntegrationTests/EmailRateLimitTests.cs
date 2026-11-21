using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EShop.Infrastructure.Identity;

namespace EShop.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class EmailRateLimitTests(ApiFactory factory)
{
    private const string Password = "Str0ng-Passw0rd-1";
    private const string ForgotPassword = "/auth/forgot-password";
    private const string Resend = "/auth/resend-confirmation";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private WebApplicationFactory<Program> CreateLimitedApp() =>
        factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:RateLimit:PermitLimit"] = "3",
            })));

    private static async Task<string> CreateConfirmedUserAsync(WebApplicationFactory<Program> app)
    {
        var email = $"{Guid.NewGuid():N}@example.com";

        using var client = app.CreateClient();
        using var response = await client.PostAsJsonAsync("/auth/register", new { email, password = Password }, Ct);
        response.EnsureSuccessStatusCode();

        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(email);
        var token = await users.GenerateEmailConfirmationTokenAsync(user!);
        Assert.True((await users.ConfirmEmailAsync(user!, token)).Succeeded);

        return email;
    }

    private int CountSentTo(string email) =>
        factory.Email.Sent.Count(message => message.To == email);

    private static async Task<HttpStatusCode> PostAsync(HttpClient client, string url, string email)
    {
        using var response = await client.PostAsJsonAsync(url, new { email }, Ct);
        return response.StatusCode;
    }

    [Fact]
    public async Task Fourth_forgot_password_request_returns_204_and_sends_nothing()
    {
        await using var app = CreateLimitedApp();
        var email = await CreateConfirmedUserAsync(app);
        using var client = app.CreateClient();
        var before = CountSentTo(email);

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.NoContent, await PostAsync(client, ForgotPassword, email));
        }

        Assert.Equal(before + 3, CountSentTo(email));

        Assert.Equal(HttpStatusCode.NoContent, await PostAsync(client, ForgotPassword, email));
        Assert.Equal(before + 3, CountSentTo(email));
    }

    [Fact]
    public async Task Resend_and_forgot_password_share_one_counter()
    {
        await using var app = CreateLimitedApp();
        var email = await CreateConfirmedUserAsync(app);
        using var client = app.CreateClient();
        var before = CountSentTo(email);

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.NoContent, await PostAsync(client, Resend, email));
        }

        Assert.Equal(HttpStatusCode.NoContent, await PostAsync(client, ForgotPassword, email));
        Assert.Equal(before, CountSentTo(email));
    }

    [Fact]
    public async Task Another_spelling_of_the_address_uses_the_same_counter()
    {
        await using var app = CreateLimitedApp();
        var email = await CreateConfirmedUserAsync(app);
        using var client = app.CreateClient();
        var before = CountSentTo(email);

        for (var i = 0; i < 3; i++)
        {
            await PostAsync(client, ForgotPassword, email);
        }

        Assert.Equal(HttpStatusCode.NoContent, await PostAsync(client, ForgotPassword, $" {email.ToUpperInvariant()} "));
        Assert.Equal(before + 3, CountSentTo(email));
    }

    [Fact]
    public async Task Limit_of_one_address_does_not_affect_another_address()
    {
        await using var app = CreateLimitedApp();
        var limited = await CreateConfirmedUserAsync(app);
        var other = await CreateConfirmedUserAsync(app);
        using var client = app.CreateClient();
        var before = CountSentTo(other);

        for (var i = 0; i < 4; i++)
        {
            await PostAsync(client, ForgotPassword, limited);
        }

        Assert.Equal(HttpStatusCode.NoContent, await PostAsync(client, ForgotPassword, other));
        Assert.Equal(before + 1, CountSentTo(other));
    }

    [Fact]
    public async Task Unknown_address_gets_the_same_status_before_and_after_the_limit()
    {
        await using var app = CreateLimitedApp();
        var email = $"{Guid.NewGuid():N}@example.com";
        using var client = app.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.NoContent, await PostAsync(client, ForgotPassword, email));
        }

        Assert.Equal(0, CountSentTo(email));
    }
}