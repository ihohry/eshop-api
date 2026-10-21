using EShop.Domain.Catalog;

namespace EShop.Domain.Orders;

public sealed class Order
{
    private readonly List<OrderItem> _items = [];

    private Order() { ShippingAddress = null!; Currency = null!; }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public OrderStatus Status { get; private set; }
    public string Currency { get; private set; }
    public decimal Total { get; private set; }
    public ShippingAddress ShippingAddress { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public IReadOnlyList<OrderItem> Items => _items;
    public DeliveryMethod DeliveryMethod { get; private set; }
    public string? PickupPointId { get; private set; }
    public string? PickupPointName { get; private set; }

    public static Order Create(Guid userId, ShippingAddress address,
        DeliveryMethod method, PickupPoint? pickupPoint,
        IReadOnlyCollection<OrderLine> lines, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
            throw new ArgumentException("An order needs at least one item.", nameof(lines));
        if (lines.Select(l => l.ProductId).Distinct().Count() != lines.Count)
            throw new ArgumentException("A product can appear only once.", nameof(lines));
        if (lines.Any(l => l.Quantity < 1 || l.UnitPrice <= 0))
            throw new ArgumentException("Quantity and price must be positive.", nameof(lines));
        if (!OrderDefaults.PhonePattern.IsMatch(address.Phone))
            throw new ArgumentException(
                "Phone must be in international format, for example +420123456789.", nameof(address));
        if (method == DeliveryMethod.PickupPoint && pickupPoint is null)
            throw new ArgumentException("A pickup point is required.", nameof(pickupPoint));
        if (method == DeliveryMethod.Courier && pickupPoint is not null)
            throw new ArgumentException("A pickup point is only for pickup delivery.", nameof(pickupPoint));

        var order = new Order
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Status = OrderStatus.PendingPayment,
            Currency = CatalogDefaults.Currency,
            ShippingAddress = address,
            CreatedAt = now,
            ExpiresAt = now + OrderDefaults.PaymentWindow,
            DeliveryMethod = method,
            PickupPointId = pickupPoint?.Id,
            PickupPointName = pickupPoint?.Name,
        };
        order._items.AddRange(lines.Select(l => new OrderItem(order.Id, l)));
        order.Total = order._items.Sum(i => i.LineTotal);
        return order;
    }

    public void MarkPaid(DateTimeOffset now)
    {
        RequirePending();
        Status = OrderStatus.Paid;
        PaidAt = now;
    }

    public void Cancel(DateTimeOffset now)
    {
        RequirePending();
        Status = OrderStatus.Cancelled;
        CancelledAt = now;
    }

    public void Expire(DateTimeOffset now)
    {
        RequirePending();
        if (now < ExpiresAt)
            throw new InvalidOperationException("The order has not expired yet.");
        Status = OrderStatus.Expired;
        CancelledAt = now;
    }

    private void RequirePending()
    {
        if (Status != OrderStatus.PendingPayment)
            throw new InvalidOperationException($"Order is {Status}, not PendingPayment.");
    }
}