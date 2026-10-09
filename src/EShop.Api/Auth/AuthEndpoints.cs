using EShop.Domain.Identity;

namespace EShop.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
            .Produces<RegisterResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> RegisterAsync(RegisterRequest request, IAccountService accounts)
    {
        var result = await accounts.RegisterCustomerAsync(request.Email, request.Password);

        if (!result.Succeeded)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(result.Errors));
        }

        // No Location header yet: there is no endpoint to read a user by id.
        return TypedResults.Json(new RegisterResponse(result.UserId!.Value), statusCode: StatusCodes.Status201Created);
    }
}