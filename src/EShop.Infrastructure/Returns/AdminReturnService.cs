using EShop.Domain.Orders;
using EShop.Domain.Payments;
using EShop.Domain.Returns;
using EShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EShop.Infrastructure.Returns;

public sealed partial class AdminReturnService(
    AppDbContext db, TimeProvider time, IPaymentGateway gateway,
    ILogger<AdminReturnService> logger) : IAdminReturnService
{
    public async Task<AdminReturnPage> ListAsync(
        ReturnStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var query = db.ReturnRequests.AsNoTracking();
        if (status is { } s)
            query = query.Where(r => r.Status == s);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new AdminReturnSummary(
                r.Id, r.OrderId, r.UserId, r.Status, r.RefundAmount, r.Items.Count, r.CreatedAt))
            .ToListAsync(ct);
        return new AdminReturnPage(items, page, pageSize, total);
    }

    public Task<ReturnRequest?> GetAsync(Guid returnId, CancellationToken ct) =>
        db.ReturnRequests.AsNoTracking().Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == returnId, ct);

    public async Task<AdminReturnChangeResult> ChangeStatusAsync(
        Guid returnId, AdminReturnAction action, string? reason, CancellationToken ct)
    {
        var request = await GetAsync(returnId, ct);
        if (request is null) return new(AdminReturnChangeStatus.NotFound);

        var (allowedFrom, to) = Rule(action);
        if (!allowedFrom.Contains(request.Status)) return Classify(to, request);
        if (action == AdminReturnAction.MarkRefunded
            && await db.Orders.AsNoTracking()
                .Where(o => o.Id == request.OrderId)
                .Select(o => o.PaymentMethod)
                .SingleAsync(ct) == PaymentMethod.Prepaid)
        {
            return await RefundPrepaidAsync(request, time.GetUtcNow(), ct);
        }
        var now = time.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var changed = await ApplyAsync(returnId, action, allowedFrom, to, reason, now, ct);
        if (changed == 0)
        {
            // A parallel request was faster: report the current state.
            await tx.RollbackAsync(ct);
            var current = await GetAsync(returnId, ct);
            return current is null
                ? new(AdminReturnChangeStatus.NotFound)
                : Classify(to, current);
        }

        if (action == AdminReturnAction.MarkReceived)
        {
            // Same product order as in checkout and cancel, so transactions cannot deadlock.
            foreach (var item in request.Items.OrderBy(i => i.ProductId))
            {
                var productId = item.ProductId;
                var quantity = item.Quantity;
                await db.Products
                    .Where(p => p.Id == productId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.StockQuantity, p => p.StockQuantity + quantity)
                        .SetProperty(p => p.UpdatedAt, now), ct);
            }
        }

        await tx.CommitAsync(ct);
        return new(AdminReturnChangeStatus.Changed, await GetAsync(returnId, ct));
    }

    private static (ReturnStatus[] AllowedFrom, ReturnStatus To) Rule(AdminReturnAction action) => action switch
    {
        AdminReturnAction.Approve => ([ReturnStatus.Requested], ReturnStatus.Approved),
        AdminReturnAction.Reject => ([ReturnStatus.Requested, ReturnStatus.Approved], ReturnStatus.Rejected),
        AdminReturnAction.MarkReceived => ([ReturnStatus.Approved], ReturnStatus.Received),
        AdminReturnAction.MarkRefunded => ([ReturnStatus.Received], ReturnStatus.Refunded),
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    // Repeating an action that is already done is not an error.
    private static AdminReturnChangeResult Classify(ReturnStatus target, ReturnRequest request) =>
        request.Status == target
            ? new(AdminReturnChangeStatus.AlreadyDone, request)
            : new(AdminReturnChangeStatus.InvalidTransition, request);

    // Each change is one conditional UPDATE: only one caller can win.
    private async Task<int> ApplyAsync(Guid returnId, AdminReturnAction action,
        ReturnStatus[] allowedFrom, ReturnStatus to, string? reason, DateTimeOffset now, CancellationToken ct)
    {
        var requests = db.ReturnRequests.Where(r => r.Id == returnId && allowedFrom.Contains(r.Status));
        return action switch
        {
            AdminReturnAction.Approve => await requests
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, to)
                    .SetProperty(r => r.DecidedAt, now), ct),
            AdminReturnAction.Reject => await requests
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, to)
                    .SetProperty(r => r.DecidedAt, now)
                    .SetProperty(r => r.RejectionReason,
                        string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()), ct),
            AdminReturnAction.MarkReceived => await requests
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, to)
                    .SetProperty(r => r.ReceivedAt, now), ct),
            AdminReturnAction.MarkRefunded => await requests
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, to)
                    .SetProperty(r => r.RefundedAt, now), ct),
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }

    // Prepaid: Stripe first, database second. The same key on every retry, so Stripe never refunds twice.
    private async Task<AdminReturnChangeResult> RefundPrepaidAsync(
        ReturnRequest request, DateTimeOffset now, CancellationToken ct)
    {
        var payment = await db.Payments.AsNoTracking()
            .Where(p => p.OrderId == request.OrderId
                && (p.Status == PaymentStatus.Paid || p.Status == PaymentStatus.Refunded))
            .OrderByDescending(p => p.PaidAt)
            .FirstOrDefaultAsync(ct);

        if (payment is null
            || payment.Status != PaymentStatus.Paid
            || string.IsNullOrEmpty(payment.ProviderPaymentReference)
            || request.RefundAmount > payment.Amount - payment.RefundedAmount)
        {
            return new(AdminReturnChangeStatus.RefundNotPossible, request);
        }

        var paymentId = payment.Id;
        var amount = request.RefundAmount;
        var refundKey = $"return-refund-{request.Id}";

        try
        {
            if (await gateway.RefundExistsAsync(payment.ProviderPaymentReference, refundKey, ct))
                LogRefundAlreadyAtProvider(logger, request.Id, paymentId);
            else
                await gateway.RefundAsync(payment.ProviderPaymentReference, amount, refundKey, ct);
        }
        catch (PaymentGatewayException)
        {
            LogRefundFailed(logger, request.Id, paymentId);
            return new(AdminReturnChangeStatus.GatewayFailed, request);
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // The return update decides the winner, so a parallel request cannot add the amount twice.
        var changed = await ApplyAsync(request.Id, AdminReturnAction.MarkRefunded,
            [ReturnStatus.Received], ReturnStatus.Refunded, null, now, ct);
        if (changed == 0)
        {
            await tx.RollbackAsync(ct);
            var current = await GetAsync(request.Id, ct);
            return current is null
                ? new(AdminReturnChangeStatus.NotFound)
                : Classify(ReturnStatus.Refunded, current);
        }

        // Partial refund first; if the amount is exactly what is left, the payment becomes Refunded.
        var updated = await db.Payments
            .Where(p => p.Id == paymentId && p.Status == PaymentStatus.Paid
                && p.RefundedAmount + amount < p.Amount)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.RefundedAmount, p => p.RefundedAmount + amount), ct);
        if (updated == 0)
        {
            updated = await db.Payments
                .Where(p => p.Id == paymentId && p.Status == PaymentStatus.Paid
                    && p.RefundedAmount + amount == p.Amount)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Status, PaymentStatus.Refunded)
                    .SetProperty(p => p.RefundedAmount, p => p.Amount), ct);
        }

        if (updated == 0)
        {
            await tx.RollbackAsync(ct);
            LogPaymentNotUpdated(logger, request.Id, paymentId);
            return new(AdminReturnChangeStatus.RefundNotPossible, await GetAsync(request.Id, ct));
        }

        await tx.CommitAsync(ct);
        LogRefunded(logger, request.Id, paymentId);
        return new(AdminReturnChangeStatus.Changed, await GetAsync(request.Id, ct));
    }

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Refund for return {ReturnId} (payment {PaymentId}) failed; the return stays Received, repeat the request")]
    private static partial void LogRefundFailed(ILogger logger, Guid returnId, Guid paymentId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Stripe refunded return {ReturnId}, but payment {PaymentId} could not be updated; check manually")]
    private static partial void LogPaymentNotUpdated(ILogger logger, Guid returnId, Guid paymentId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Return {ReturnId} was refunded (payment {PaymentId})")]
    private static partial void LogRefunded(ILogger logger, Guid returnId, Guid paymentId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Stripe already holds the refund of return {ReturnId} (payment {PaymentId}); only the database is updated")]
    private static partial void LogRefundAlreadyAtProvider(ILogger logger, Guid returnId, Guid paymentId);
}