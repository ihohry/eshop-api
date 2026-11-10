using EShop.Domain.Catalog;
using EShop.Domain.Orders;

namespace EShop.Domain.UnitTests.Orders;

public class OrderCreateTests
{
    [Fact]
    public void Prepaid_courier_order_starts_as_pending_payment()
    {
        var lines = new[] { OrderTestData.Line(10m, 2), OrderTestData.Line(5.50m, 1) };

        var order = OrderTestData.Create(lines: lines);

        Assert.NotEqual(Guid.Empty, order.Id);
        Assert.Equal(OrderTestData.UserId, order.UserId);
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Equal(CatalogDefaults.Currency, order.Currency);
        Assert.Equal(25.50m, order.Total);
        Assert.Equal(2, order.Items.Count);
        Assert.Equal(OrderTestData.Now, order.CreatedAt);
        Assert.Equal(OrderTestData.Now + OrderTestData.PaymentWindow, order.ExpiresAt);
        Assert.Equal(DeliveryMethod.Courier, order.DeliveryMethod);
        Assert.Null(order.PickupPointId);
        Assert.Null(order.PaidAt);
    }

    [Fact]
    public void Pay_on_receipt_pickup_order_starts_as_pending_confirmation()
    {
        var order = OrderTestData.Create(PaymentMethod.PayOnReceipt);

        Assert.Equal(OrderStatus.PendingConfirmation, order.Status);
        Assert.Equal(PaymentMethod.PayOnReceipt, order.PaymentMethod);
        Assert.Equal(DeliveryMethod.PickupPoint, order.DeliveryMethod);
        Assert.Equal("PP-1", order.PickupPointId);
        Assert.Equal("Point one", order.PickupPointName);
    }

    [Fact]
    public void Prepaid_pickup_order_is_allowed()
    {
        var order = OrderTestData.Create(PaymentMethod.Prepaid, OrderTestData.Pickup());

        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Equal("PP-1", order.PickupPointId);
    }

    [Fact]
    public void Order_without_lines_is_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            OrderTestData.Create(lines: Array.Empty<OrderLine>()));
    }

    [Fact]
    public void Duplicate_product_is_rejected()
    {
        var line = OrderTestData.Line();

        Assert.Throws<ArgumentException>(() =>
            OrderTestData.Create(lines: new[] { line, line with { Quantity = 2 } }));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, -5)]
    public void Non_positive_quantity_or_price_is_rejected(int quantity, int price)
    {
        var lines = new[] { OrderTestData.Line(price, quantity) };

        Assert.Throws<ArgumentException>(() => OrderTestData.Create(lines: lines));
    }

    [Theory]
    [InlineData("420123456789")]
    [InlineData("+0123456789")]
    [InlineData("+42012")]
    [InlineData("+4201234567890123")]
    [InlineData("+420 123 456 789")]
    [InlineData("")]
    public void Phone_outside_international_format_is_rejected(string phone)
    {
        Assert.Throws<ArgumentException>(() =>
            OrderTestData.Create(address: OrderTestData.Address(phone)));
    }

    [Fact]
    public void Pickup_delivery_without_pickup_point_is_rejected()
    {
        var delivery = new OrderDelivery(DeliveryMethod.PickupPoint, null);

        Assert.Throws<ArgumentException>(() =>
            OrderTestData.Create(PaymentMethod.Prepaid, delivery));
    }

    [Fact]
    public void Courier_delivery_with_pickup_point_is_rejected()
    {
        var delivery = new OrderDelivery(DeliveryMethod.Courier, new PickupPoint("PP-1", "Point one"));

        Assert.Throws<ArgumentException>(() =>
            OrderTestData.Create(PaymentMethod.Prepaid, delivery));
    }

    [Fact]
    public void Courier_delivery_with_pay_on_receipt_is_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            OrderTestData.Create(PaymentMethod.PayOnReceipt, OrderTestData.Courier()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_payment_window_is_rejected(int minutes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OrderTestData.Create(paymentWindow: TimeSpan.FromMinutes(minutes)));
    }
}