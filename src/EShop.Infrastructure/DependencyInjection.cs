using EShop.Domain.Identity;
using EShop.Infrastructure.Identity;
using EShop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;
using EShop.Infrastructure.Email;

namespace EShop.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var database = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.CommandTimeout(database.CommandTimeoutSeconds));

            if (database.EnableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }
        });

        services.AddIdentityServices(configuration);

        return services;
    }

    private static IServiceCollection AddIdentityServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<PasswordPolicyOptions>()
            .Bind(configuration.GetSection(PasswordPolicyOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.Configure<DataProtectionTokenProviderOptions>(
            options => options.TokenLifespan = TimeSpan.FromHours(24));

        services.AddOptions<EmailConfirmationOptions>()
            .BindConfiguration(EmailConfirmationOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();
            
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();

        // The password rules come from our validated options, not from hard-coded values.
        services.AddOptions<IdentityOptions>()
            .Configure<IOptions<PasswordPolicyOptions>>((identity, policy) =>
            {
                var password = policy.Value;

                identity.Password.RequiredLength = password.RequiredLength;
                identity.Password.RequiredUniqueChars = password.RequiredUniqueChars;
                identity.Password.RequireDigit = password.RequireDigit;
                identity.Password.RequireLowercase = password.RequireLowercase;
                identity.Password.RequireUppercase = password.RequireUppercase;
                identity.Password.RequireNonAlphanumeric = password.RequireNonAlphanumeric;
            });

        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddOptions<EmailOptions>()
                .BindConfiguration(EmailOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();
        services.AddSingleton<EShop.Domain.Email.IEmailSender, SmtpEmailSender>();

        return services;
    }
}