namespace EShop.Api.Auth;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/ping", () => Results.Ok(new { message = "pong" }))
            .RequireAuthorization(Policies.AdminOnly)
            .WithName("AdminPing")
            .ExcludeFromDescription();

        return app;
    }
}