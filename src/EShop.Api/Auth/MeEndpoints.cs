using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace EShop.Api.Auth;

public sealed record MeResponse(Guid Id, string Email, IReadOnlyList<string> Roles);

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me", (ClaimsPrincipal user) =>
            {
                var id = Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
                var email = user.FindFirstValue(JwtRegisteredClaimNames.Email)!;
                var roles = user.FindAll("role").Select(claim => claim.Value).ToArray();

                return Results.Ok(new MeResponse(id, email, roles));
            })
            .RequireAuthorization()
            .WithName("GetMe")
            .Produces<MeResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }
}