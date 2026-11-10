using EShop.Domain.Orders;

namespace EShop.Domain.UnitTests.Orders;

internal static class OrderTestData
{
    public static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    public static readonly TimeSpan PaymentWindow = TimeSpan.FromHours(24);
    public static readonly Guid UserId = Guid.Parse("0196a5c0-0000-7000-8000-000000000001");

    public static ShippingAddress Address(string phone = "+420123456789") =>
        new("Jan Novak", phone, "Main street 1", null, "11000", "Prague", "CZ");

    public static OrderDelivery Courier() => new(DeliveryMethod.Courier, null);

    public static OrderDelivery Pickup() =>
        new(DeliveryMethod.PickupPoint, new PickupPoint("PP-1", "Point one"));

    public static OrderLine Line(decimal price = 10m, int quantity = 1) =>
        new(Guid.NewGuid(), "Product", price, quantity);

    public static Order Create(
        PaymentMethod paymentMethod = PaymentMethod.Prepaid,
        OrderDelivery? delivery = null,
        ShippingAddress? address = null,
        IReadOnlyCollection<OrderLine>? lines = null,
        TimeSpan? paymentWindow = null) =>
        Order.Create(
            UserId,
            address ?? Address(),
            delivery ?? DefaultDelivery(paymentMethod),
            paymentMethod,
            lines ?? new[] { Line() },
            Now,
            paymentWindow ?? PaymentWindow);

    private static OrderDelivery DefaultDelivery(PaymentMethod paymentMethod) =>
        paymentMethod == PaymentMethod.PayOnReceipt ? Pickup() : Courier();
}