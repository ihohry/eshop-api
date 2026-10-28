using EShop.Api.Auth;
using System.ComponentModel.DataAnnotations;
using EShop.Domain.Exceptions;
using EShop.Domain.Returns;

namespace EShop.Api.Returns;

public sealed record RejectReturnBody(
    [property: StringLength(ReturnRequest.MaxRejectionReasonLength)] string? Reason);

public sealed record AdminReturnSummaryResponse(
    Guid Id, Guid OrderId, Guid UserId, string Status, decimal RefundAmount,
    int ItemCount, DateTimeOffset CreatedAt);

public sealed record AdminReturnPageResponse(
    IReadOnlyList<AdminReturnSummaryResponse> Items, int Page, int PageSize, int TotalCount);

public sealed record AdminReturnResponse(
    Guid Id, Guid OrderId, Guid UserId, string Status, string Reason, string? RejectionReason,
    decimal RefundAmount, DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt,
    DateTimeOffset? ReceivedAt, DateTimeOffset? RefundedAt, IReadOnlyList<ReturnItemResponse> Items);

public static class AdminReturnEndpoints
{
    public static IEndpointRouteBuilder MapAdminReturnEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/returns")
            .RequireAuthorization(Policies.AdminOnly)
            .WithTags("Admin returns");

        group.MapGet("/", async (ReturnStatus? status, IAdminReturnService returns, CancellationToken ct,
            [Range(1, int.MaxValue)] int page = 1, [Range(1, 50)] int pageSize = 20) =>
        {
            var result = await returns.ListAsync(status, page, pageSize, ct);
            return Results.Ok(new AdminReturnPageResponse(
                result.Items.Select(i => new AdminReturnSummaryResponse(
                    i.Id, i.OrderId, i.UserId, i.Status.ToString(), i.RefundAmount, i.ItemCount, i.CreatedAt)).ToList(),
                result.Page, result.PageSize, result.Total));
        });

        group.MapGet("/{id:guid}", async (Guid id, IAdminReturnService returns, CancellationToken ct) =>
            await returns.GetAsync(id, ct) is { } r
                ? Results.Ok(ToResponse(r))
                : throw new NotFoundException("Return not found."));

        group.MapPost("/{id:guid}/approve", (Guid id, IAdminReturnService returns, CancellationToken ct) =>
            ChangeAsync(returns, id, AdminReturnAction.Approve, null, ct));

        group.MapPost("/{id:guid}/reject", (Guid id, RejectReturnBody? body,
            IAdminReturnService returns, CancellationToken ct) =>
            ChangeAsync(returns, id, AdminReturnAction.Reject, body?.Reason, ct));

        group.MapPost("/{id:guid}/receive", (Guid id, IAdminReturnService returns, CancellationToken ct) =>
            ChangeAsync(returns, id, AdminReturnAction.MarkReceived, null, ct));

        group.MapPost("/{id:guid}/refund", (Guid id, IAdminReturnService returns, CancellationToken ct) =>
            ChangeAsync(returns, id, AdminReturnAction.MarkRefunded, null, ct));

        return app;
    }

    private static async Task<IResult> ChangeAsync(IAdminReturnService returns, Guid id,
        AdminReturnAction action, string? reason, CancellationToken ct)
    {
        var result = await returns.ChangeStatusAsync(id, action, reason, ct);
        return result.Status switch
        {
            AdminReturnChangeStatus.Changed or AdminReturnChangeStatus.AlreadyDone =>
                Results.Ok(ToResponse(result.Return!)),
            AdminReturnChangeStatus.NotFound => throw new NotFoundException("Return not found."),
            _ => throw new ConflictException(
                $"The return is {result.Return!.Status}; this action is not allowed."),
        };
    }

    private static AdminReturnResponse ToResponse(ReturnRequest r) => new(
        r.Id, r.OrderId, r.UserId, r.Status.ToString(), r.Reason, r.RejectionReason, r.RefundAmount,
        r.CreatedAt, r.DecidedAt, r.ReceivedAt, r.RefundedAt,
        r.Items.Select(i => new ReturnItemResponse(i.ProductId, i.Quantity)).ToList());
}