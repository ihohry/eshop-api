using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

using EShop.Infrastructure.Persistence;

namespace EShop.Api.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17").Build();

    public async ValueTask InitializeAsync()
    {
        await _db.StartAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _db.GetConnectionString());
        Environment.SetEnvironmentVariable("Identity__Jwt__SigningKey", "test-signing-key-with-at-least-32-characters!");
        Environment.SetEnvironmentVariable("Email__Host", "localhost");
        Environment.SetEnvironmentVariable("Email__Port", "1025");
        Environment.SetEnvironmentVariable("Email__Security", "None");
        Environment.SetEnvironmentVariable("Email__FromAddress", "noreply@test.local");
        Environment.SetEnvironmentVariable("Email__Confirmation__UrlTemplate", "https://test.local/confirm?userId={userId}&token={token}");
        Environment.SetEnvironmentVariable("Email__PasswordReset__UrlTemplate", "https://test.local/reset?userId={userId}&token={token}");
        Environment.SetEnvironmentVariable("Stripe__SecretKey", "sk_test_dummy");
        Environment.SetEnvironmentVariable("Stripe__WebhookSecret", "whsec_dummy");
        Environment.SetEnvironmentVariable("Stripe__SuccessUrlTemplate", "https://test.local/success?orderId={orderId}");
        Environment.SetEnvironmentVariable("Stripe__CancelUrlTemplate", "https://test.local/cancel?orderId={orderId}");

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseEnvironment("Testing");

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }
}