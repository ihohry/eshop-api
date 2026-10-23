using EShop.Api.Auth;
using EShop.Domain.Orders;

namespace EShop.Api.Orders;

public static class AdminOrderEndpoints
{
    public static IEndpointRouteBuilder MapAdminOrderEndpoints(this IEndpointRouteBuilder app)
    {
        // Add the same `using` for Policies as in CatalogAdminEndpoints.
        var group = app.MapGroup("/admin/orders")
            .RequireAuthorization(Policies.AdminOnly)
            .WithTags("Admin orders");

        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("/{id:guid}/mark-paid", (Guid id, IAdminOrderService s, CancellationToken ct) =>
            ChangeAsync(s, id, AdminOrderAction.MarkPaid, ct));
        group.MapPost("/{id:guid}/ship", (Guid id, IAdminOrderService s, CancellationToken ct) =>
            ChangeAsync(s, id, AdminOrderAction.Ship, ct));
        group.MapPost("/{id:guid}/deliver", (Guid id, IAdminOrderService s, CancellationToken ct) =>
            ChangeAsync(s, id, AdminOrderAction.Deliver, ct));
        group.MapPost("/{id:guid}/cancel", (Guid id, IAdminOrderService s, CancellationToken ct) =>
            ChangeAsync(s, id, AdminOrderAction.Cancel, ct));
        return app;
    }

    private static async Task<IResult> ListAsync(
        IAdminOrderService orders, CancellationToken ct,
        string? status = null, int page = 1, int pageSize = 20)
    {
        OrderStatus? filter = null;
        if (status is not null)
        {
            if (!Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsed)
                || !Enum.IsDefined(parsed))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = [$"Unknown status. Use one of: {string.Join(", ", Enum.GetNames<OrderStatus>())}."],
                });
            }
            filter = parsed;
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var result = await orders.ListAsync(filter, page, pageSize, ct);
        return TypedResults.Ok(new AdminOrderPageResponse(
            result.Items.Select(o => new AdminOrderSummaryResponse(
                o.Id, o.UserId, o.Status.ToString(), o.Currency, o.Total, o.ItemCount, o.CreatedAt)).ToList(),
            result.Page, result.PageSize, result.TotalCount));
    }

    private static async Task<IResult> GetAsync(
        Guid id, IAdminOrderService orders, CancellationToken ct)
    {
        var order = await orders.GetAsync(id, ct);
        return order is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(new AdminOrderResponse(order.UserId, order.ToResponse()));
    }

    private static async Task<IResult> ChangeAsync(
        IAdminOrderService orders, Guid id, AdminOrderAction action, CancellationToken ct)
    {
        var result = await orders.ChangeStatusAsync(id, action, ct);
        return result.Status switch
        {
            AdminOrderChangeStatus.Changed or AdminOrderChangeStatus.AlreadyDone =>
                TypedResults.Ok(new AdminOrderResponse(result.Order!.UserId, result.Order.ToResponse())),
            AdminOrderChangeStatus.NotFound => TypedResults.NotFound(),
            AdminOrderChangeStatus.InvalidTransition => TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The status change is not allowed.",
                detail: $"The order is {result.Order!.Status}."),
            _ => throw new InvalidOperationException($"Unexpected status {result.Status}."),
        };
    }
}