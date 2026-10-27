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
    public DateTimeOffset? ShippedAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public IReadOnlyList<OrderItem> Items => _items;
    public DeliveryMethod DeliveryMethod { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public string? PickupPointId { get; private set; }
    public string? PickupPointName { get; private set; }

    public static Order Create(Guid userId, ShippingAddress address, OrderDelivery delivery,
        PaymentMethod paymentMethod, IReadOnlyCollection<OrderLine> lines,
        DateTimeOffset now, TimeSpan paymentWindow)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(lines);
        var (method, pickupPoint) = delivery;
        if (paymentWindow <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(paymentWindow), "Payment window must be positive.");
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
            throw new ArgumentException("A pickup point is required.", nameof(delivery));
        if (method == DeliveryMethod.Courier && pickupPoint is not null)
            throw new ArgumentException("A pickup point is only for pickup delivery.", nameof(delivery));
        if (method == DeliveryMethod.Courier && paymentMethod != PaymentMethod.Prepaid)
            throw new ArgumentException("Courier delivery allows only prepaid orders.", nameof(paymentMethod));

        var order = new Order
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Status = paymentMethod == PaymentMethod.Prepaid
                ? OrderStatus.PendingPayment
                : OrderStatus.PendingConfirmation,
            Currency = CatalogDefaults.Currency,
            ShippingAddress = address,
            CreatedAt = now,
            ExpiresAt = now + paymentWindow,
            DeliveryMethod = method,
            PaymentMethod = paymentMethod,
            PickupPointId = pickupPoint?.Id,
            PickupPointName = pickupPoint?.Name,
        };
        order._items.AddRange(lines.Select(l => new OrderItem(order.Id, l)));
        order.Total = order._items.Sum(i => i.LineTotal);
        return order;
    }

    public void MarkPaid(DateTimeOffset now)
    {
        RequireStatus(OrderStatus.PendingPayment);
        Status = OrderStatus.Paid;
        PaidAt = now;
    }

    public void Confirm(DateTimeOffset now)
    {
        RequireStatus(OrderStatus.PendingConfirmation);
        Status = OrderStatus.Confirmed;
        ConfirmedAt = now;
    }

    public void Refuse(DateTimeOffset now)
    {
        if (PaymentMethod != PaymentMethod.PayOnReceipt)
            throw new InvalidOperationException("Only pay-on-receipt orders can be refused.");
        if (Status is not (OrderStatus.PendingConfirmation or OrderStatus.Confirmed or OrderStatus.Shipped))
            throw new InvalidOperationException($"Order is {Status} and cannot be refused.");
        Status = OrderStatus.Refused;
        CancelledAt = now;
    }

    public void Cancel(DateTimeOffset now)
    {
        if (Status is not (OrderStatus.PendingPayment or OrderStatus.Paid
            or OrderStatus.PendingConfirmation or OrderStatus.Confirmed))
            throw new InvalidOperationException($"Order is {Status} and cannot be cancelled.");
        Status = OrderStatus.Cancelled;
        CancelledAt = now;
    }

    public void MarkShipped(DateTimeOffset now)
    {
        RequireStatus(PaymentMethod == PaymentMethod.Prepaid ? OrderStatus.Paid : OrderStatus.Confirmed);
        Status = OrderStatus.Shipped;
        ShippedAt = now;
    }

    public void MarkDelivered(DateTimeOffset now)
    {
        RequireStatus(OrderStatus.Shipped);
        Status = OrderStatus.Delivered;
        DeliveredAt = now;
        if (PaymentMethod == PaymentMethod.PayOnReceipt)
            PaidAt = now; // the customer pays when the parcel is handed over
    }

    public void Expire(DateTimeOffset now)
    {
        if (Status is not (OrderStatus.PendingPayment or OrderStatus.PendingConfirmation))
            throw new InvalidOperationException($"Order is {Status} and cannot expire.");
        if (now < ExpiresAt)
            throw new InvalidOperationException("The order has not expired yet.");
        Status = OrderStatus.Expired;
        CancelledAt = now;
    }

    public DateTimeOffset? ReturnableUntil(TimeSpan window) =>
        Status == OrderStatus.Delivered && DeliveredAt is { } deliveredAt
            ? deliveredAt + window
            : null;

    private void RequireStatus(OrderStatus expected)
    {
        if (Status != expected)
            throw new InvalidOperationException($"Order is {Status}, not {expected}.");
    }
}