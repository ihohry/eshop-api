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

        group.MapPost("/login", async (
                LoginRequest request,
                IAccountService accountService,
                ITokenService tokenService) =>
            {
                var result = await accountService.LoginAsync(request.Email, request.Password);

                return result.Status switch
                {
                    LoginStatus.Succeeded => Results.Ok(ToResponse(tokenService.CreateAccessToken(result.User!))),
                    LoginStatus.LockedOut => Results.Problem(
                        title: "Account temporarily locked",
                        detail: "Too many failed login attempts. Try again later.",
                        statusCode: StatusCodes.Status423Locked),
                    _ => Results.Problem(
                        title: "Invalid credentials",
                        detail: "The email or password is incorrect.",
                        statusCode: StatusCodes.Status401Unauthorized),
                };
            })
            .WithName("Login")
            .Produces<LoginResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status423Locked);

            static LoginResponse ToResponse(AccessToken token) => new(token.Value, "Bearer", token.ExpiresAt);

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