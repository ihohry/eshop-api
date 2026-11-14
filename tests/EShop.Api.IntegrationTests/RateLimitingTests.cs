using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace EShop.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class RateLimitingTests(ApiFactory factory)
{
    private const int Limit = 3;

    private static readonly object Credentials = new { email = "a@b.c", password = "wrong" };

    private WebApplicationFactory<Program> CreateApp(int permitLimit, int windowSeconds = 60) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:AuthPermitLimit"] = permitLimit.ToString(CultureInfo.InvariantCulture),
                ["RateLimiting:AuthWindowSeconds"] = windowSeconds.ToString(CultureInfo.InvariantCulture),
            })));

    private static async Task<HttpStatusCode> PostStatusAsync(HttpClient client, string url, object body)
    {
        using var response = await client.PostAsJsonAsync(url, body, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    [Fact]
    public async Task Login_over_the_limit_returns_429_with_retry_after()
    {
        await using var app = CreateApp(Limit);
        using var client = app.CreateClient();

        for (var i = 0; i < Limit; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, await PostStatusAsync(client, "/auth/login", Credentials));
        }

        using var response = await client.PostAsJsonAsync(
            "/auth/login", Credentials, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var retryAfter = int.Parse(response.Headers.GetValues("Retry-After").Single(), CultureInfo.InvariantCulture);
        Assert.InRange(retryAfter, 1, 60);
    }

    [Theory]
    [InlineData("/auth/register")]
    [InlineData("/auth/login")]
    [InlineData("/auth/confirm-email")]
    [InlineData("/auth/resend-confirmation")]
    [InlineData("/auth/forgot-password")]
    [InlineData("/auth/reset-password")]
    public async Task Limited_endpoint_returns_429_after_the_limit(string url)
    {
        await using var app = CreateApp(Limit);
        using var client = app.CreateClient();

        for (var i = 0; i < Limit; i++)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests, await PostStatusAsync(client, url, new { }));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, await PostStatusAsync(client, url, new { }));
    }

    [Fact]
    public async Task Limited_endpoints_share_one_limit_per_client()
    {
        await using var app = CreateApp(Limit);
        using var client = app.CreateClient();

        await PostStatusAsync(client, "/auth/login", Credentials);
        await PostStatusAsync(client, "/auth/login", Credentials);
        await PostStatusAsync(client, "/auth/register", new { });

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            await PostStatusAsync(client, "/auth/forgot-password", new { }));
    }

    [Theory]
    [InlineData("/auth/refresh")]
    [InlineData("/auth/logout")]
    public async Task Refresh_and_logout_are_not_limited(string url)
    {
        await using var app = CreateApp(Limit);
        using var client = app.CreateClient();

        for (var i = 0; i < Limit * 3; i++)
        {
            Assert.NotEqual(
                HttpStatusCode.TooManyRequests,
                await PostStatusAsync(client, url, new { refreshToken = "invalid" }));
        }
    }

    [Fact]
    public async Task Other_endpoints_are_not_limited()
    {
        await using var app = CreateApp(Limit);
        using var client = app.CreateClient();

        for (var i = 0; i < Limit * 3; i++)
        {
            using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task Limit_is_lifted_when_the_window_ends()
    {
        await using var app = CreateApp(permitLimit: 2, windowSeconds: 2);
        using var client = app.CreateClient();

        await PostStatusAsync(client, "/auth/login", Credentials);
        await PostStatusAsync(client, "/auth/login", Credentials);
        Assert.Equal(HttpStatusCode.TooManyRequests, await PostStatusAsync(client, "/auth/login", Credentials));

        await Task.Delay(TimeSpan.FromSeconds(2.5), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, await PostStatusAsync(client, "/auth/login", Credentials));
    }
}