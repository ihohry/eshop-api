using System.Net;
using System.Net.Http.Json;
using EShop.Domain.Email;
using EShop.Infrastructure.Email;
using EShop.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EShop.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class AccountEmailDispatchTests(ApiFactory factory)
{
    private const string Password = "Str0ng-Passw0rd-1";
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<string> CreateUserAsync(bool confirmed)
    {
        var email = $"{Guid.NewGuid():N}@example.com";

        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/auth/register", new { email, password = Password }, Ct);
        response.EnsureSuccessStatusCode();

        if (confirmed)
        {
            using var scope = factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByEmailAsync(email);
            var token = await users.GenerateEmailConfirmationTokenAsync(user!);
            Assert.True((await users.ConfirmEmailAsync(user!, token)).Succeeded);
        }

        return email;
    }

    private int CountSentTo(string email) =>
        factory.Email.Sent.Count(message => message.To == email);

    [Theory]
    [InlineData("/auth/resend-confirmation", false)]
    [InlineData("/auth/forgot-password", true)]
    public async Task Known_address_dispatches_exactly_one_email(string url, bool confirmed)
    {
        var email = await CreateUserAsync(confirmed);
        var before = CountSentTo(email);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(url, new { email }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(before + 1, CountSentTo(email));
    }

    [Theory]
    [InlineData("/auth/resend-confirmation")]
    [InlineData("/auth/forgot-password")]
    public async Task Unknown_address_dispatches_nothing(string url)
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(url, new { email }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, CountSentTo(email));
    }

    [Theory]
    [InlineData("/auth/resend-confirmation", false)]
    [InlineData("/auth/forgot-password", true)]
    public async Task Known_address_is_answered_while_the_sender_is_blocked(string url, bool confirmed)
    {
        var email = await CreateUserAsync(confirmed);
        var sender = new BlockedEmailSender();
        await using var app = CreateAppWithRealDispatcher(sender);
        using var client = app.CreateClient();

        using var response = await client.PostAsJsonAsync(url, new { email }, Ct)
            .WaitAsync(WaitLimit, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await sender.Started.WaitAsync(WaitLimit, Ct);
    }

    private WebApplicationFactory<Program> CreateAppWithRealDispatcher(IEmailSender sender) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton(sender);

            services.RemoveAll<IEmailDispatcher>();
            services.AddSingleton<IEmailDispatcher>(
                provider => provider.GetRequiredService<BackgroundEmailDispatcher>());
        }));

    private sealed class BlockedEmailSender : IEmailSender
    {
        private readonly TaskCompletionSource _started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            _started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }
}