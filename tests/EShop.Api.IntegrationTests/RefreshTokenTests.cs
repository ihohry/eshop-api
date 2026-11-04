using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using EShop.Api.Auth;
using EShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EShop.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class RefreshTokenTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string token) =>
        client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(token), Ct);

    private static Task<HttpResponseMessage> LogoutAsync(HttpClient client, string token) =>
        client.PostAsJsonAsync("/auth/logout", new RefreshTokenRequest(token), Ct);

    private static async Task<string> RefreshOkAsync(HttpClient client, string token)
    {
        using var response = await RefreshAsync(client, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(Ct);
        Assert.NotNull(body);
        return body.RefreshToken;
    }

    private static async Task AssertRefreshRejectedAsync(HttpClient client, string token)
    {
        using var response = await RefreshAsync(client, token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_returns_new_tokens_and_the_old_token_stops_working()
    {
        var user = await factory.RegisterAndLoginAsync();
        using var client = factory.CreateClient();

        var second = await RefreshOkAsync(client, user.RefreshToken);
        Assert.NotEqual(user.RefreshToken, second);

        var third = await RefreshOkAsync(client, second);
        Assert.NotEqual(second, third);

        await AssertRefreshRejectedAsync(client, user.RefreshToken);
    }

    [Fact]
    public async Task Reuse_of_a_rotated_token_revokes_the_whole_family()
    {
        var user = await factory.RegisterAndLoginAsync();
        using var client = factory.CreateClient();

        var second = await RefreshOkAsync(client, user.RefreshToken);

        await AssertRefreshRejectedAsync(client, user.RefreshToken);
        await AssertRefreshRejectedAsync(client, second);
    }

    [Fact]
    public async Task Concurrent_refresh_with_the_same_token_has_one_winner()
    {
        for (var round = 0; round < 10; round++)
        {
            var user = await factory.RegisterAndLoginAsync();
            var gate = new TaskCompletionSource();

            async Task<HttpResponseMessage> SendAsync()
            {
                using var client = factory.CreateClient();
                await gate.Task;
                return await RefreshAsync(client, user.RefreshToken);
            }

            var first = Task.Run(SendAsync, Ct);
            var second = Task.Run(SendAsync, Ct);
            gate.SetResult();
            var responses = await Task.WhenAll(first, second);

            try
            {
                var winners = responses.Where(r => r.StatusCode == HttpStatusCode.OK).ToList();
                Assert.Single(winners);
                Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Unauthorized);

                var body = await winners[0].Content.ReadFromJsonAsync<LoginResponse>(Ct);
                Assert.NotNull(body);

                using var client = factory.CreateClient();
                await AssertRefreshRejectedAsync(client, body.RefreshToken);
            }
            finally
            {
                foreach (var response in responses)
                {
                    response.Dispose();
                }
            }
        }
    }

    [Fact]
    public async Task Logout_revokes_the_token()
    {
        var user = await factory.RegisterAndLoginAsync();
        using var client = factory.CreateClient();

        using var logout = await LogoutAsync(client, user.RefreshToken);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        await AssertRefreshRejectedAsync(client, user.RefreshToken);
    }

    [Fact]
    public async Task Logout_with_a_rotated_token_revokes_the_whole_family()
    {
        var user = await factory.RegisterAndLoginAsync();
        using var client = factory.CreateClient();
        var second = await RefreshOkAsync(client, user.RefreshToken);

        using var logout = await LogoutAsync(client, user.RefreshToken);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        await AssertRefreshRejectedAsync(client, second);
    }

    [Fact]
    public async Task Logout_with_an_unknown_token_returns_no_content()
    {
        using var client = factory.CreateClient();

        using var logout = await LogoutAsync(client, "unknown-token");

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
    }

    [Fact]
    public async Task Refresh_with_an_unknown_token_returns_unauthorized()
    {
        using var client = factory.CreateClient();

        await AssertRefreshRejectedAsync(client, "unknown-token");
    }

    [Fact]
    public async Task Refresh_with_an_empty_token_returns_bad_request()
    {
        using var client = factory.CreateClient();

        using var response = await RefreshAsync(client, string.Empty);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_with_an_expired_token_returns_unauthorized()
    {
        var user = await factory.RegisterAndLoginAsync();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user.RefreshToken)));
        var past = DateTimeOffset.UtcNow.AddDays(-1);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var changed = await db.RefreshTokens
                .Where(t => t.TokenHash == hash)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiresAt, past), Ct);
            Assert.Equal(1, changed);
        }

        using var client = factory.CreateClient();
        await AssertRefreshRejectedAsync(client, user.RefreshToken);
    }
}