using System.Net.Http.Headers;
using System.Net.Http.Json;
using EShop.Api.Auth;
using EShop.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace EShop.Api.IntegrationTests;

public sealed record TestUser(Guid Id, string Email, string Password, string AccessToken, string RefreshToken);

public static class AuthHelper
{
    public const string DefaultPassword = "Test-Password1!";

    public static async Task<TestUser> RegisterAndLoginAsync(
        this ApiFactory factory,
        string? email = null,
        string password = DefaultPassword)
    {
        var ct = TestContext.Current.CancellationToken;
        email ??= $"user-{Guid.NewGuid():N}@test.local";
        using var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync("/auth/register", new RegisterRequest(email, password), ct);
        register.EnsureSuccessStatusCode();

        Guid userId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByEmailAsync(email)
                ?? throw new InvalidOperationException("The registered user was not found.");
            var token = await users.GenerateEmailConfirmationTokenAsync(user);
            var confirmed = await users.ConfirmEmailAsync(user, token);
            if (!confirmed.Succeeded)
            {
                throw new InvalidOperationException("Email confirmation failed in the test helper.");
            }

            userId = user.Id;
        }

        var login = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password), ct);
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>(ct)
            ?? throw new InvalidOperationException("The login response was empty.");

        return new TestUser(userId, email, password, body.AccessToken, body.RefreshToken);
    }

    public static HttpClient CreateAuthorizedClient(this ApiFactory factory, TestUser user)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
        return client;
    }
}