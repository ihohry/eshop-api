using EShop.Infrastructure.Orders;
using Microsoft.Extensions.Options;
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
        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("/{id:guid}/cancel", CancelAsync);
        return app;
    }

    private static async Task<IResult> CheckoutAsync(
        CheckoutRequest request, ClaimsPrincipal user, ICheckoutService checkout,
        IOptions<OrderOptions> options, CancellationToken ct)
    {
        var method = Enum.Parse<DeliveryMethod>(request.DeliveryMethod!);

        var paymentMethod = request.PaymentMethod is null
            ? PaymentMethod.Prepaid
            : Enum.Parse<PaymentMethod>(request.PaymentMethod, ignoreCase: true);

        if (method == DeliveryMethod.Courier && paymentMethod == PaymentMethod.PayOnReceipt)
            return FieldError("paymentMethod", "Pay on receipt is only for pickup point delivery.");
        if (method == DeliveryMethod.PickupPoint && request.PickupPoint is null)
            return FieldError("pickupPoint", "A pickup point is required.");
        if (method == DeliveryMethod.Courier && request.PickupPoint is not null)
            return FieldError("pickupPoint", "A pickup point is only for pickup delivery.");

        var a = request.ShippingAddress!;
        var address = new ShippingAddress(
            a.FullName.Trim(), a.Phone.Trim(), a.Line1.Trim(),
            string.IsNullOrWhiteSpace(a.Line2) ? null : a.Line2.Trim(),
            a.PostalCode.Trim(), a.City.Trim(), a.CountryCode);
        var pickup = request.PickupPoint is { } p ? new PickupPoint(p.Id.Trim(), p.Name.Trim()) : null;

        var result = await checkout.CheckoutAsync(
            GetUserId(user), address, method, pickup, paymentMethod, ct);

        return result.Status switch
        {
            CheckoutStatus.Created => TypedResults.Created(
                $"/orders/{result.Order!.Id}", result.Order.ToResponse(options.Value.ReturnWindow)),
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
            CheckoutStatus.PayOnReceiptNotAvailable => TypedResults.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Pay on receipt is not available.",
                detail: "Choose another payment method."),
            CheckoutStatus.PayOnReceiptLimitExceeded => TypedResults.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The order total is too high for pay on receipt.",
                detail: "Choose another payment method or reduce the order."),
            _ => throw new InvalidOperationException($"Unexpected status {result.Status}."),
        };
    }

    private static async Task<IResult> ListAsync(
    ClaimsPrincipal user, IOrderService orders, CancellationToken ct,
    int page = 1, int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var result = await orders.ListAsync(GetUserId(user), page, pageSize, ct);
        return TypedResults.Ok(new OrderPageResponse(
            result.Items.Select(o => new OrderSummaryResponse(
                o.Id, o.Status.ToString(), o.Currency, o.Total, o.ItemCount, o.CreatedAt)).ToList(),
            result.Page, result.PageSize, result.TotalCount));
    }

    private static async Task<IResult> GetAsync(
        Guid id, ClaimsPrincipal user, IOrderService orders,
        IOptions<OrderOptions> options, CancellationToken ct)
    {
        var order = await orders.GetAsync(GetUserId(user), id, ct);
        return order is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(order.ToResponse(options.Value.ReturnWindow));
    }

    private static async Task<IResult> CancelAsync(
        Guid id, ClaimsPrincipal user, IOrderService orders,
        IOptions<OrderOptions> options, CancellationToken ct)
    {
        var result = await orders.CancelAsync(GetUserId(user), id, ct);
        return result.Status switch
        {
            CancelOrderStatus.Cancelled or CancelOrderStatus.AlreadyCancelled =>
                TypedResults.Ok(result.Order!.ToResponse(options.Value.ReturnWindow)),
            CancelOrderStatus.NotFound => TypedResults.NotFound(),
            CancelOrderStatus.NotCancellable => TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The order cannot be cancelled.",
                detail: "Only orders waiting for payment can be cancelled."),
            _ => throw new InvalidOperationException($"Unexpected status {result.Status}."),
        };
    }

    private static Guid GetUserId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue("sub") ?? throw new InvalidOperationException("Missing sub claim."));

    private static ValidationProblem FieldError(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}