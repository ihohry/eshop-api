using EShop.Domain.Orders;
using EShop.Domain.Returns;
using EShop.Domain.UnitTests.Orders;

namespace EShop.Domain.UnitTests.Returns;

public class ReturnRequestTests
{
    [Fact]
    public void New_request_is_requested_with_items_and_refund_amount()
    {
        var order = ReturnTestData.DeliveredOrder();
        var lines = new[]
        {
            new ReturnLine(ReturnTestData.ProductA, 2),
            new ReturnLine(ReturnTestData.ProductB, 1),
        };

        var request = ReturnTestData.Create(order, "  Broken  ", lines);

        Assert.NotEqual(Guid.Empty, request.Id);
        Assert.Equal(order.Id, request.OrderId);
        Assert.Equal(order.UserId, request.UserId);
        Assert.Equal(ReturnStatus.Requested, request.Status);
        Assert.Equal("Broken", request.Reason);
        Assert.Equal(25.50m, request.RefundAmount);
        Assert.Equal(ReturnTestData.Now, request.CreatedAt);
        Assert.Null(request.RejectionReason);
        Assert.Null(request.DecidedAt);
        Assert.Null(request.ReceivedAt);
        Assert.Null(request.RefundedAt);
        Assert.Equal(2, request.Items.Count);
        Assert.Contains(request.Items, i =>
            i.ReturnRequestId == request.Id && i.ProductId == ReturnTestData.ProductA && i.Quantity == 2);
        Assert.Contains(request.Items, i =>
            i.ReturnRequestId == request.Id && i.ProductId == ReturnTestData.ProductB && i.Quantity == 1);
    }

    [Fact]
    public void Missing_arguments_are_rejected()
    {
        var order = ReturnTestData.DeliveredOrder();
        var lines = new[] { new ReturnLine(ReturnTestData.ProductA, 1) };
        var returned = Array.Empty<ReturnedQuantity>();

        Assert.Throws<ArgumentNullException>(() =>
            ReturnRequest.Create(null!, "Broken", lines, returned, ReturnTestData.Now, ReturnTestData.Window));
        Assert.Throws<ArgumentNullException>(() =>
            ReturnRequest.Create(order, "Broken", null!, returned, ReturnTestData.Now, ReturnTestData.Window));
        Assert.Throws<ArgumentNullException>(() =>
            ReturnRequest.Create(order, "Broken", lines, null!, ReturnTestData.Now, ReturnTestData.Window));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Blank_reason_is_rejected(string reason)
    {
        Assert.Throws<ArgumentException>(() => ReturnTestData.Create(reason: reason));
    }

    [Fact]
    public void Reason_is_limited_to_the_maximum_length()
    {
        var accepted = ReturnTestData.Create(reason: new string('x', ReturnRequest.MaxReasonLength));

        Assert.Equal(ReturnRequest.MaxReasonLength, accepted.Reason.Length);
        Assert.Throws<ArgumentException>(() =>
            ReturnTestData.Create(reason: new string('x', ReturnRequest.MaxReasonLength + 1)));
    }

    [Fact]
    public void Empty_lines_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => ReturnTestData.Create(lines: Array.Empty<ReturnLine>()));
    }

    [Fact]
    public void Duplicate_product_is_rejected()
    {
        var lines = new[]
        {
            new ReturnLine(ReturnTestData.ProductA, 1),
            new ReturnLine(ReturnTestData.ProductA, 1),
        };

        Assert.Throws<ArgumentException>(() => ReturnTestData.Create(lines: lines));
    }

    [Theory]
    [InlineData(OrderStatus.PendingPayment)]
    [InlineData(OrderStatus.Paid)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Cancelled)]
    public void Only_delivered_orders_can_be_returned(OrderStatus status)
    {
        var order = OrderTestData.InStatus(status);
        var lines = new[] { new ReturnLine(order.Items[0].ProductId, 1) };

        Assert.Throws<InvalidOperationException>(() => ReturnTestData.Create(order, lines: lines));
    }

    [Fact]
    public void Return_is_allowed_exactly_at_the_deadline()
    {
        var request = ReturnTestData.Create(now: ReturnTestData.Deadline);

        Assert.Equal(ReturnTestData.Deadline, request.CreatedAt);
    }

    [Fact]
    public void Return_after_the_deadline_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ReturnTestData.Create(now: ReturnTestData.Deadline.AddTicks(1)));
    }

    [Fact]
    public void Product_outside_the_order_is_rejected()
    {
        var lines = new[] { new ReturnLine(Guid.NewGuid(), 1) };

        Assert.Throws<ArgumentException>(() => ReturnTestData.Create(lines: lines));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Quantity_below_one_is_rejected(int quantity)
    {
        var lines = new[] { new ReturnLine(ReturnTestData.ProductA, quantity) };

        Assert.Throws<ArgumentException>(() => ReturnTestData.Create(lines: lines));
    }

    [Fact]
    public void Quantity_is_limited_to_the_quantity_bought()
    {
        var accepted = ReturnTestData.Create(lines: new[] { new ReturnLine(ReturnTestData.ProductA, 3) });

        Assert.Equal(30m, accepted.RefundAmount);
        Assert.Throws<ArgumentException>(() =>
            ReturnTestData.Create(lines: new[] { new ReturnLine(ReturnTestData.ProductA, 4) }));
    }

    [Fact]
    public void Quantity_already_returned_counts_against_the_limit()
    {
        var returned = new[] { new ReturnedQuantity(ReturnTestData.ProductA, 2) };

        var accepted = ReturnTestData.Create(
            lines: new[] { new ReturnLine(ReturnTestData.ProductA, 1) }, alreadyReturned: returned);

        Assert.Equal(10m, accepted.RefundAmount);
        Assert.Throws<ArgumentException>(() => ReturnTestData.Create(
            lines: new[] { new ReturnLine(ReturnTestData.ProductA, 2) }, alreadyReturned: returned));
    }

    [Fact]
    public void Several_earlier_returns_of_one_product_are_added_up()
    {
        var returned = new[]
        {
            new ReturnedQuantity(ReturnTestData.ProductA, 1),
            new ReturnedQuantity(ReturnTestData.ProductA, 1),
        };

        Assert.Throws<ArgumentException>(() => ReturnTestData.Create(
            lines: new[] { new ReturnLine(ReturnTestData.ProductA, 2) }, alreadyReturned: returned));
    }

    [Fact]
    public void Earlier_returns_of_another_product_do_not_count()
    {
        var returned = new[] { new ReturnedQuantity(ReturnTestData.ProductB, 2) };

        var request = ReturnTestData.Create(
            lines: new[] { new ReturnLine(ReturnTestData.ProductA, 3) }, alreadyReturned: returned);

        Assert.Equal(30m, request.RefundAmount);
    }

    [Fact]
    public void Approve_sets_status_and_decision_time()
    {
        var request = ReturnTestData.InStatus(ReturnStatus.Requested);

        request.Approve(ReturnTestData.LaterDecision);

        Assert.Equal(ReturnStatus.Approved, request.Status);
        Assert.Equal(ReturnTestData.LaterDecision, request.DecidedAt);
    }

    [Theory]
    [InlineData(ReturnStatus.Approved)]
    [InlineData(ReturnStatus.Received)]
    [InlineData(ReturnStatus.Refunded)]
    [InlineData(ReturnStatus.Rejected)]
    public void Approve_is_allowed_only_from_requested(ReturnStatus status)
    {
        var request = ReturnTestData.InStatus(status);

        Assert.Throws<InvalidOperationException>(() => request.Approve(ReturnTestData.LaterDecision));
        Assert.Equal(status, request.Status);
    }

    [Theory]
    [InlineData(ReturnStatus.Requested)]
    [InlineData(ReturnStatus.Approved)]
    public void Reject_stores_status_time_and_trimmed_reason(ReturnStatus from)
    {
        var request = ReturnTestData.InStatus(from);

        request.Reject(ReturnTestData.LaterDecision, "  Used item  ");

        Assert.Equal(ReturnStatus.Rejected, request.Status);
        Assert.Equal(ReturnTestData.LaterDecision, request.DecidedAt);
        Assert.Equal("Used item", request.RejectionReason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Reject_without_a_reason_stores_no_reason(string? reason)
    {
        var request = ReturnTestData.InStatus(ReturnStatus.Requested);

        request.Reject(ReturnTestData.LaterDecision, reason);

        Assert.Equal(ReturnStatus.Rejected, request.Status);
        Assert.Null(request.RejectionReason);
    }

    [Fact]
    public void Rejecting_an_approved_return_overwrites_the_decision_time()
    {
        var request = ReturnTestData.InStatus(ReturnStatus.Approved);
        Assert.Equal(ReturnTestData.Now, request.DecidedAt);

        request.Reject(ReturnTestData.LaterDecision);

        Assert.Equal(ReturnTestData.LaterDecision, request.DecidedAt);
    }

    [Theory]
    [InlineData(ReturnStatus.Received)]
    [InlineData(ReturnStatus.Refunded)]
    [InlineData(ReturnStatus.Rejected)]
    public void Reject_is_not_allowed_after_receive_or_a_final_status(ReturnStatus status)
    {
        var request = ReturnTestData.InStatus(status);
        var rejectionReason = request.RejectionReason;

        Assert.Throws<InvalidOperationException>(() => request.Reject(ReturnTestData.LaterDecision, "Late"));
        Assert.Equal(status, request.Status);
        Assert.Equal(rejectionReason, request.RejectionReason);
    }

    [Fact]
    public void Mark_received_sets_status_and_time()
    {
        var request = ReturnTestData.InStatus(ReturnStatus.Approved);

        request.MarkReceived(ReturnTestData.LaterDecision);

        Assert.Equal(ReturnStatus.Received, request.Status);
        Assert.Equal(ReturnTestData.LaterDecision, request.ReceivedAt);
    }

    [Theory]
    [InlineData(ReturnStatus.Requested)]
    [InlineData(ReturnStatus.Received)]
    [InlineData(ReturnStatus.Refunded)]
    [InlineData(ReturnStatus.Rejected)]
    public void Mark_received_is_allowed_only_from_approved(ReturnStatus status)
    {
        var request = ReturnTestData.InStatus(status);

        Assert.Throws<InvalidOperationException>(() => request.MarkReceived(ReturnTestData.LaterDecision));
        Assert.Equal(status, request.Status);
    }

    [Fact]
    public void Mark_refunded_sets_status_and_time()
    {
        var request = ReturnTestData.InStatus(ReturnStatus.Received);

        request.MarkRefunded(ReturnTestData.LaterDecision);

        Assert.Equal(ReturnStatus.Refunded, request.Status);
        Assert.Equal(ReturnTestData.LaterDecision, request.RefundedAt);
    }

    [Theory]
    [InlineData(ReturnStatus.Requested)]
    [InlineData(ReturnStatus.Approved)]
    [InlineData(ReturnStatus.Refunded)]
    [InlineData(ReturnStatus.Rejected)]
    public void Mark_refunded_is_allowed_only_from_received(ReturnStatus status)
    {
        var request = ReturnTestData.InStatus(status);

        Assert.Throws<InvalidOperationException>(() => request.MarkRefunded(ReturnTestData.LaterDecision));
        Assert.Equal(status, request.Status);
    }
}