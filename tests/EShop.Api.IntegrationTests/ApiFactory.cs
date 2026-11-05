using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;
using Xunit;
using EShop.Api.IntegrationTests.Fakes;

using EShop.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using EShop.Domain.Payments;

namespace EShop.Api.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17").Build();
    private Respawner? _respawner;

    public FakeTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);
    public FakeEmailSender Email { get; } = new();
    public FakePaymentGateway Payments { get; } = new();
    public string ImageRoot { get; } = Path.Combine(Path.GetTempPath(), $"eshop-test-images-{Guid.NewGuid():N}");

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
        Directory.CreateDirectory(ImageRoot);
        Environment.SetEnvironmentVariable("ImageStorage__RootPath", ImageRoot);

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();

        await using var connection = new NpgsqlConnection(_db.GetConnectionString());
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore =
            [
                new Table("public", "__EFMigrationsHistory"),
                new Table("public", "AspNetRoles")
            ]
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(_db.GetConnectionString());
        await connection.OpenAsync();
        await _respawner!.ResetAsync(connection);
        Email.Reset();
        Payments.Reset();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);

            services.RemoveAll<EShop.Domain.Email.IEmailSender>();
            services.AddSingleton<EShop.Domain.Email.IEmailSender>(Email);

            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(Payments);
        });
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
        if (Directory.Exists(ImageRoot))
        {
            Directory.Delete(ImageRoot, recursive: true);
        }
    }
}