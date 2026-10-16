using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using EShop.Domain.Carts;

namespace EShop.Api.Carts;

public sealed record AddCartItemRequest(
    Guid ProductId,
    [property: Range(1, CartDefaults.MaxQuantityPerItem)] int Quantity);

public sealed record SetCartItemQuantityRequest(
    [property: Range(1, CartDefaults.MaxQuantityPerItem)] int Quantity);

public static class CartEndpoints
{
    public static IEndpointRouteBuilder MapCartEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/cart").WithTags("Cart").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, ICartService carts, CancellationToken ct) =>
            Results.Ok(await carts.GetAsync(GetUserId(user), ct)));

        group.MapPost("/items", async (
            AddCartItemRequest request, ClaimsPrincipal user, ICartService carts, CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            var result = await carts.AddItemAsync(userId, request.ProductId, request.Quantity, ct);
            return result == CartOperationResult.Ok
                ? Results.Ok(await carts.GetAsync(userId, ct))
                : ToProblem(result);
        });

        group.MapPut("/items/{productId:guid}", async (
            Guid productId, SetCartItemQuantityRequest request,
            ClaimsPrincipal user, ICartService carts, CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            var result = await carts.SetQuantityAsync(userId, productId, request.Quantity, ct);
            return result == CartOperationResult.Ok
                ? Results.Ok(await carts.GetAsync(userId, ct))
                : ToProblem(result);
        });

        group.MapDelete("/items/{productId:guid}", async (
            Guid productId, ClaimsPrincipal user, ICartService carts, CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            await carts.RemoveItemAsync(userId, productId, ct);
            return Results.Ok(await carts.GetAsync(userId, ct));
        });

        group.MapDelete("/", async (ClaimsPrincipal user, ICartService carts, CancellationToken ct) =>
        {
            await carts.ClearAsync(GetUserId(user), ct);
            return Results.NoContent();
        });

        return app;
    }

    private static Guid GetUserId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue("sub") ?? throw new InvalidOperationException("Missing sub claim."));

    private static IResult ToProblem(CartOperationResult result) => result switch
    {
        CartOperationResult.ProductNotFound => Results.Problem(
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "Product not available",
            detail: "The product does not exist or is not available."),
        CartOperationResult.ItemNotFound => Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Cart item not found",
            detail: "The product is not in your cart."),
        CartOperationResult.LimitExceeded => Results.Problem(
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "Cart limit exceeded",
            detail: $"A cart can hold at most {CartDefaults.MaxQuantityPerItem} per product "
                + $"and {CartDefaults.MaxDistinctItems} different products."),
        _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
    };
}