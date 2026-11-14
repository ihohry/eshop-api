using System.Net;
using System.Net.Http.Json;
using EShop.Api.Auth;
using EShop.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace EShop.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class LoginTimingTests(ApiFactory factory) : IAsyncLifetime
{
    private const string WrongPassword = "Wrong-Password1!";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => new(factory.ResetDatabaseAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Unknown_email_does_one_password_check()
    {
        var hasher = new CountingPasswordHasher();
        using var host = CreateHost(hasher);
        using var client = host.CreateClient();

        using var response = await PostLoginAsync(client, "nobody@test.local", WrongPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, hasher.VerifyCalls);
    }

    [Fact]
    public async Task Wrong_password_does_one_password_check()
    {
        var user = await factory.RegisterAndLoginAsync();
        var hasher = new CountingPasswordHasher();
        using var host = CreateHost(hasher);
        using var client = host.CreateClient();

        using var response = await PostLoginAsync(client, user.Email, WrongPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, hasher.VerifyCalls);
    }

    [Fact]
    public async Task Locked_account_does_one_password_check()
    {
        var user = await factory.RegisterAndLoginAsync();
        var hasher = new CountingPasswordHasher();
        using var host = CreateHost(hasher);
        using var client = host.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var failed = await PostLoginAsync(client, user.Email, WrongPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        hasher.Reset();
        using var response = await PostLoginAsync(client, user.Email, user.Password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, hasher.VerifyCalls);
    }

    private WebApplicationFactory<Program> CreateHost(CountingPasswordHasher hasher) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPasswordHasher<ApplicationUser>>();
            services.AddSingleton<IPasswordHasher<ApplicationUser>>(hasher);
        }));

    private static Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password), Ct);

    private sealed class CountingPasswordHasher : IPasswordHasher<ApplicationUser>
    {
        private readonly PasswordHasher<ApplicationUser> inner = new();
        private int verifyCalls;

        public int VerifyCalls => Volatile.Read(ref verifyCalls);

        public void Reset() => Interlocked.Exchange(ref verifyCalls, 0);

        public string HashPassword(ApplicationUser user, string password) =>
            inner.HashPassword(user, password);

        public PasswordVerificationResult VerifyHashedPassword(
            ApplicationUser user, string hashedPassword, string providedPassword)
        {
            Interlocked.Increment(ref verifyCalls);
            return inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
    }
}