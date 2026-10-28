using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using EShop.Domain.Returns;

namespace EShop.Api.Returns;

public sealed record ReturnLineBody(Guid ProductId, [property: Range(1, 10000)] int Quantity);

public sealed record CreateReturnBody(
    [property: Required, StringLength(ReturnRequest.MaxReasonLength)] string Reason,
    [property: Required] IReadOnlyList<ReturnLineBody> Items);

public sealed record ReturnItemResponse(Guid ProductId, int Quantity);

public sealed record ReturnResponse(
    Guid Id, Guid OrderId, string Status, string Reason, string? RejectionReason,
    decimal RefundAmount, DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt,
    DateTimeOffset? ReceivedAt, DateTimeOffset? RefundedAt, IReadOnlyList<ReturnItemResponse> Items);

public static class ReturnEndpoints
{
    public static IEndpointRouteBuilder MapReturnEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders/{orderId:guid}/returns")
            .RequireAuthorization()
            .WithTags("Returns");

        group.MapPost("/", async (Guid orderId, CreateReturnBody body, ClaimsPrincipal user,
            IReturnService returns, CancellationToken ct) =>
        {
            var lines = body.Items.Select(i => new ReturnLine(i.ProductId, i.Quantity)).ToList();
            var created = await returns.CreateAsync(GetUserId(user), orderId, body.Reason, lines, ct);
            return Results.Created($"/orders/{orderId}/returns", ToResponse(created));
        });

        group.MapGet("/", async (Guid orderId, ClaimsPrincipal user,
            IReturnService returns, CancellationToken ct) =>
            Results.Ok((await returns.ListForOrderAsync(GetUserId(user), orderId, ct)).Select(ToResponse)));

        return app;
    }

    private static Guid GetUserId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue("sub") ?? throw new InvalidOperationException("Missing sub claim."));

    private static ReturnResponse ToResponse(ReturnRequest r) => new(
        r.Id, r.OrderId, r.Status.ToString(), r.Reason, r.RejectionReason, r.RefundAmount, r.CreatedAt,
        r.DecidedAt, r.ReceivedAt, r.RefundedAt,
        r.Items.Select(i => new ReturnItemResponse(i.ProductId, i.Quantity)).ToList());
}