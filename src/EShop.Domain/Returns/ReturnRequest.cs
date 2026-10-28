using EShop.Domain.Orders;

namespace EShop.Domain.Returns;

public sealed class ReturnRequest
{
    public const int MaxReasonLength = 1000;
    public const int MaxRejectionReasonLength = 500;

    private readonly List<ReturnItem> _items = [];

    private ReturnRequest() { Reason = null!; }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid UserId { get; private set; }
    public ReturnStatus Status { get; private set; }
    public string Reason { get; private set; }
    public string? RejectionReason { get; private set; }
    public decimal RefundAmount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public DateTimeOffset? ReceivedAt { get; private set; }
    public DateTimeOffset? RefundedAt { get; private set; }
    public IReadOnlyList<ReturnItem> Items => _items;

    public static ReturnRequest Create(Order order, string reason,
    IReadOnlyCollection<ReturnLine> lines, IReadOnlyCollection<ReturnedQuantity> alreadyReturned,
    DateTimeOffset now, TimeSpan returnWindow)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(alreadyReturned);
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > MaxReasonLength)
            throw new ArgumentException(
                $"Reason is required and at most {MaxReasonLength} characters.", nameof(reason));
        if (lines.Count == 0)
            throw new ArgumentException("A return needs at least one item.", nameof(lines));
        if (lines.Select(l => l.ProductId).Distinct().Count() != lines.Count)
            throw new ArgumentException("A product can appear only once.", nameof(lines));
        if (order.ReturnableUntil(returnWindow) is not { } until)
            throw new InvalidOperationException("Only delivered orders can be returned.");
        if (now > until)
            throw new InvalidOperationException("The return window has closed.");

        decimal refund = 0;
        foreach (var line in lines)
        {
            var item = order.Items.FirstOrDefault(i => i.ProductId == line.ProductId)
                ?? throw new ArgumentException("A product is not part of the order.", nameof(lines));
            var returned = alreadyReturned.Where(r => r.ProductId == line.ProductId).Sum(r => r.Quantity);
            if (line.Quantity < 1 || returned + line.Quantity > item.Quantity)
                throw new ArgumentException("Invalid return quantity.", nameof(lines));
            refund += item.UnitPrice * line.Quantity;
        }

        var request = new ReturnRequest
        {
            Id = Guid.CreateVersion7(),
            OrderId = order.Id,
            UserId = order.UserId,
            Status = ReturnStatus.Requested,
            Reason = reason.Trim(),
            RefundAmount = refund,
            CreatedAt = now,
        };
        request._items.AddRange(lines.Select(l => new ReturnItem(request.Id, l)));
        return request;
    }

    public void Approve(DateTimeOffset now)
    {
        RequireStatus(ReturnStatus.Requested);
        Status = ReturnStatus.Approved;
        DecidedAt = now;
    }

    public void Reject(DateTimeOffset now, string? reason = null)
    {
        RequireStatus(ReturnStatus.Requested);
        Status = ReturnStatus.Rejected;
        DecidedAt = now;
        RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    public void MarkReceived(DateTimeOffset now)
    {
        RequireStatus(ReturnStatus.Approved);
        Status = ReturnStatus.Received;
        ReceivedAt = now;
    }

    public void MarkRefunded(DateTimeOffset now)
    {
        RequireStatus(ReturnStatus.Received);
        Status = ReturnStatus.Refunded;
        RefundedAt = now;
    }

    private void RequireStatus(ReturnStatus expected)
    {
        if (Status != expected)
            throw new InvalidOperationException($"Return is {Status}, not {expected}.");
    }
}