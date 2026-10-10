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
                ITokenService tokenService,
                CancellationToken ct) =>
            {
                var result = await accountService.LoginAsync(request.Email, request.Password, ct);

                return result.Status switch
                {
                    LoginStatus.Succeeded => Results.Ok(ToResponse(tokenService, result)),
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

        group.MapPost("/refresh", async (
                RefreshTokenRequest request,
                IAccountService accountService,
                ITokenService tokenService,
                CancellationToken ct) =>
            {
                var result = await accountService.RefreshAsync(request.RefreshToken, ct);

                return result.Status == LoginStatus.Succeeded
                    ? Results.Ok(ToResponse(tokenService, result))
                    : Results.Problem(
                        title: "Invalid refresh token",
                        detail: "The refresh token is invalid or expired.",
                        statusCode: StatusCodes.Status401Unauthorized);
            })
            .WithName("Refresh")
            .Produces<LoginResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", async (
                RefreshTokenRequest request,
                IAccountService accountService,
                CancellationToken ct) =>
            {
                await accountService.LogoutAsync(request.RefreshToken, ct);
                return Results.NoContent();
            })
            .WithName("Logout")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static LoginResponse ToResponse(ITokenService tokenService, LoginResult result)
    {
        var token = tokenService.CreateAccessToken(result.User!);
        return new LoginResponse(token.Value, "Bearer", token.ExpiresAt, result.RefreshToken!);
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