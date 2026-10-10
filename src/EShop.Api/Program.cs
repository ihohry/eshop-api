using EShop.Api.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using EShop.Api.Common;
using EShop.Infrastructure;
using EShop.Infrastructure.Persistence;
using Serilog;
using Scalar.AspNetCore;
using EShop.Domain.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddValidation();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services.ConfigureOptions<ConfigureJwtBearerOptions>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.AdminOnly, policy => policy.RequireRole(Roles.Admin));

var app = builder.Build();

app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();            
    app.MapScalarApiReference(); 
}
else
{
    app.UseHttpsRedirection();
}

app.MapHealthChecks("/health");
app.MapAuthEndpoints();
app.MapMeEndpoints();
app.MapAdminEndpoints();

app.Run();

public partial class Program { } // needed later for integration tests