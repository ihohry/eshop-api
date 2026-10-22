using Microsoft.AspNetCore.Http.HttpResults;
using System.Security.Claims;
using EShop.Domain.Orders;

namespace EShop.Api.Orders;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders").RequireAuthorization().WithTags("Orders");
        group.MapPost("/", CheckoutAsync);
        return app;
    }

    private static async Task<IResult> CheckoutAsync(
        CheckoutRequest request, ClaimsPrincipal user, ICheckoutService checkout, CancellationToken ct)
    {
        var method = Enum.Parse<DeliveryMethod>(request.DeliveryMethod!);

        if (method == DeliveryMethod.PickupPoint && request.PickupPoint is null)
            return PickupError("A pickup point is required.");
        if (method == DeliveryMethod.Courier && request.PickupPoint is not null)
            return PickupError("A pickup point is only for pickup delivery.");

        var a = request.ShippingAddress!;
        var address = new ShippingAddress(
            a.FullName.Trim(), a.Phone.Trim(), a.Line1.Trim(),
            string.IsNullOrWhiteSpace(a.Line2) ? null : a.Line2.Trim(),
            a.PostalCode.Trim(), a.City.Trim(), a.CountryCode);
        var pickup = request.PickupPoint is { } p ? new PickupPoint(p.Id.Trim(), p.Name.Trim()) : null;

        var result = await checkout.CheckoutAsync(GetUserId(user), address, method, pickup, ct);

        return result.Status switch
        {
            CheckoutStatus.Created => TypedResults.Created(
                $"/orders/{result.Order!.Id}", result.Order.ToResponse()),
            CheckoutStatus.EmptyCart => TypedResults.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The cart is empty.",
                detail: "Add at least one product before checkout."),
            CheckoutStatus.ItemsUnavailable => TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Some items are not available.",
                detail: "Change the quantity or remove these lines, then try again.",
                extensions: new Dictionary<string, object?>
                {
                    ["lines"] = result.Problems!.Select(x => new CheckoutProblemResponse(
                        x.ProductId, x.ProductName, x.Quantity, x.Reason.ToString())).ToList(),
                }),
            CheckoutStatus.CartChanged => TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The cart changed during checkout.",
                detail: "Please check your cart and try again."),
            _ => throw new InvalidOperationException($"Unexpected status {result.Status}."),
        };
    }

    private static Guid GetUserId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue("sub") ?? throw new InvalidOperationException("Missing sub claim."));

    private static ValidationProblem PickupError(string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["pickupPoint"] = [message] });
}