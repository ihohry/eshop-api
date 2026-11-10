using EShop.Domain.Orders;

namespace EShop.Domain.UnitTests.Orders;

public class OrderTransitionTests
{
    private static readonly DateTimeOffset At = OrderTestData.Now.AddDays(2);

    [Fact]
    public void MarkPaid_from_pending_payment_sets_status_and_time()
    {
        var order = OrderTestData.InStatus(OrderStatus.PendingPayment);

        order.MarkPaid(At);

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(At, order.PaidAt);
    }

    [Theory]
    [InlineData(OrderStatus.Paid, PaymentMethod.Prepaid)]
    [InlineData(OrderStatus.Shipped, PaymentMethod.Prepaid)]
    [InlineData(OrderStatus.Delivered, PaymentMethod.Prepaid)]
    [InlineData(OrderStatus.Cancelled, PaymentMethod.Prepaid)]
    [InlineData(OrderStatus.Expired, PaymentMethod.Prepaid)]
    [InlineData(OrderStatus.PendingConfirmation, PaymentMethod.PayOnReceipt)]
    public void MarkPaid_from_other_status_is_rejected(OrderStatus status, PaymentMethod method)
    {
        var order = OrderTestData.InStatus(status, method);

        Assert.Throws<InvalidOperationException>(() => order.MarkPaid(At));
        Assert.Equal(status, order.Status);
    }

    [Fact]
    public void Confirm_from_pending_confirmation_sets_status_and_time()
    {
        var order = OrderTestData.InStatus(OrderStatus.PendingConfirmation, PaymentMethod.PayOnReceipt);

        order.Confirm(At);

        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Equal(At, order.ConfirmedAt);
    }

    [Theory]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Refused)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Expired)]
    public void Confirm_from_other_status_is_rejected(OrderStatus status)
    {
        var order = OrderTestData.InStatus(status, PaymentMethod.PayOnReceipt);

        Assert.Throws<InvalidOperationException>(() => order.Confirm(At));
        Assert.Equal(status, order.Status);
    }

    [Fact]
    public void Confirm_on_prepaid_order_is_rejected()
    {
        var order = OrderTestData.InStatus(OrderStatus.PendingPayment);

        Assert.Throws<InvalidOperationException>(() => order.Confirm(At));
    }

    [Theory]
    [InlineData(OrderStatus.PendingConfirmation)]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Shipped)]
    public void Refuse_pay_on_receipt_order_sets_status_and_time(OrderStatus status)
    {
        var order = OrderTestData.InStatus(status, PaymentMethod.PayOnReceipt);

        order.Refuse(At);

        Assert.Equal(OrderStatus.Refused, order.Status);
        Assert.Equal(At, order.CancelledAt);
    }

    [Theory]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Refused)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Expired)]
    public void Refuse_pay_on_receipt_order_from_other_status_is_rejected(OrderStatus status)
    {
        var order = OrderTestData.InStatus(status, PaymentMethod.PayOnReceipt);

        Assert.Throws<InvalidOperationException>(() => order.Refuse(At));
        Assert.Equal(status, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.PendingPayment)]
    [InlineData(OrderStatus.Paid)]
    [InlineData(OrderStatus.Shipped)]
    public void Refuse_prepaid_order_is_rejected(OrderStatus status)
    {
        var order = OrderTestData.InStatus(status);

        Assert.Throws<InvalidOperationException>(() => order.Refuse(At));
        Assert.Equal(status, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.PendingPayment, PaymentMethod.Prepaid)]
    [InlineData(OrderStatus.Paid, PaymentMethod.Prepaid)]
    [InlineData(OrderStatus.PendingConfirmation, PaymentMethod.PayOnReceipt)]
    [InlineData(OrderStatus.Confirmed, PaymentMethod.PayOnReceipt)]
    public void Cancel_sets_status_and_time(OrderStatus status, PaymentMethod method)
    {
        var order = OrderTestData.InStatus(status, method);

        order.Cancel(At);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(At, order.CancelledAt);
    }

    [Theory]
    [InlineData(OrderStatus.Shipped, PaymentMethod.Prepaid)]
    [InlineData(OrderStatus.Delivered, PaymentMethod.Prepaid)]
    [InlineData(OrderStatus.Cancelled, PaymentMethod.Prepaid)]
    [InlineData(OrderStatus.Expired, PaymentMethod.Prepaid)]
    [InlineData(OrderStatus.Shipped, PaymentMethod.PayOnReceipt)]
    [InlineData(OrderStatus.Refused, PaymentMethod.PayOnReceipt)]
    public void Cancel_from_other_status_is_rejected(OrderStatus status, PaymentMethod method)
    {
        var order = OrderTestData.InStatus(status, method);

        Assert.Throws<InvalidOperationException>(() => order.Cancel(At));
        Assert.Equal(status, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Paid, PaymentMethod.Prepaid)]
    [InlineData(OrderStatus.Confirmed, PaymentMethod.PayOnReceipt)]
    public void MarkShipped_from_ready_status_sets_status_and_time(OrderStatus status, PaymentMethod method)
    {
        var order = OrderTestData.InStatus(status, method);

        order.MarkShipped(At);

        Assert.Equal(OrderStatus.Shipped, order.Status);
        Assert.Equal(At, order.ShippedAt);
    }

    [Theory]
    [InlineData(OrderStatus.PendingPayment, PaymentMethod.Prepaid)]
    [InlineData(OrderStatus.Shipped, PaymentMethod.Prepaid)]
    [InlineData(OrderStatus.Delivered, PaymentMethod.Prepaid)]
    [InlineData(OrderStatus.Cancelled, PaymentMethod.Prepaid)]
    [InlineData(OrderStatus.PendingConfirmation, PaymentMethod.PayOnReceipt)]
    [InlineData(OrderStatus.Shipped, PaymentMethod.PayOnReceipt)]
    [InlineData(OrderStatus.Refused, PaymentMethod.PayOnReceipt)]
    public void MarkShipped_from_other_status_is_rejected(OrderStatus status, PaymentMethod method)
    {
        var order = OrderTestData.InStatus(status, method);

        Assert.Throws<InvalidOperationException>(() => order.MarkShipped(At));
        Assert.Equal(status, order.Status);
    }

    [Fact]
    public void MarkDelivered_prepaid_keeps_the_original_payment_time()
    {
        var order = OrderTestData.InStatus(OrderStatus.Shipped);

        order.MarkDelivered(At);

        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(At, order.DeliveredAt);
        Assert.Equal(OrderTestData.Later, order.PaidAt);
    }

    [Fact]
    public void MarkDelivered_pay_on_receipt_sets_the_payment_time()
    {
        var order = OrderTestData.InStatus(OrderStatus.Shipped, PaymentMethod.PayOnReceipt);
        Assert.Null(order.PaidAt);

        order.MarkDelivered(At);

        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(At, order.PaidAt);
    }

    [Theory]
    [InlineData(OrderStatus.Paid, PaymentMethod.Prepaid)]
    [InlineData(OrderStatus.Delivered, PaymentMethod.Prepaid)]
    [InlineData(OrderStatus.Cancelled, PaymentMethod.Prepaid)]
    [InlineData(OrderStatus.Confirmed, PaymentMethod.PayOnReceipt)]
    public void MarkDelivered_from_other_status_is_rejected(OrderStatus status, PaymentMethod method)
    {
        var order = OrderTestData.InStatus(status, method);

        Assert.Throws<InvalidOperationException>(() => order.MarkDelivered(At));
        Assert.Equal(status, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.PendingPayment, PaymentMethod.Prepaid)]
    [InlineData(OrderStatus.PendingConfirmation, PaymentMethod.PayOnReceipt)]
    public void Expire_at_the_deadline_sets_status_and_time(OrderStatus status, PaymentMethod method)
    {
        var order = OrderTestData.InStatus(status, method);

        order.Expire(order.ExpiresAt);

        Assert.Equal(OrderStatus.Expired, order.Status);
        Assert.Equal(order.ExpiresAt, order.CancelledAt);
    }

    [Fact]
    public void Expire_before_the_deadline_is_rejected()
    {
        var order = OrderTestData.InStatus(OrderStatus.PendingPayment);

        Assert.Throws<InvalidOperationException>(() => order.Expire(order.ExpiresAt.AddTicks(-1)));
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Paid, PaymentMethod.Prepaid)]
    [InlineData(OrderStatus.Shipped, PaymentMethod.Prepaid)]
    [InlineData(OrderStatus.Cancelled, PaymentMethod.Prepaid)]
    [InlineData(OrderStatus.Confirmed, PaymentMethod.PayOnReceipt)]
    [InlineData(OrderStatus.Refused, PaymentMethod.PayOnReceipt)]
    public void Expire_from_other_status_is_rejected(OrderStatus status, PaymentMethod method)
    {
        var order = OrderTestData.InStatus(status, method);

        Assert.Throws<InvalidOperationException>(() => order.Expire(order.ExpiresAt));
        Assert.Equal(status, order.Status);
    }

    [Fact]
    public void ReturnableUntil_of_a_delivered_order_is_delivery_time_plus_window()
    {
        var order = OrderTestData.InStatus(OrderStatus.Shipped);
        order.MarkDelivered(At);

        var result = order.ReturnableUntil(TimeSpan.FromDays(14));

        Assert.Equal(At.AddDays(14), result);
    }

    [Theory]
    [InlineData(OrderStatus.PendingPayment)]
    [InlineData(OrderStatus.Paid)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Cancelled)]
    public void ReturnableUntil_of_an_undelivered_order_is_null(OrderStatus status)
    {
        var order = OrderTestData.InStatus(status);

        Assert.Null(order.ReturnableUntil(TimeSpan.FromDays(14)));
    }
}