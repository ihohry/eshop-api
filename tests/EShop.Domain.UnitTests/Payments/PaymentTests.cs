using EShop.Domain.Catalog;
using EShop.Domain.Payments;

namespace EShop.Domain.UnitTests.Payments;

public class PaymentTests
{
    [Fact]
    public void New_payment_is_pending_with_session_data()
    {
        var payment = PaymentTestData.Pending(50m);

        Assert.NotEqual(Guid.Empty, payment.Id);
        Assert.Equal(PaymentTestData.OrderId, payment.OrderId);
        Assert.Equal(PaymentProvider.Stripe, payment.Provider);
        Assert.Equal("cs_test_1", payment.ProviderSessionId);
        Assert.Equal("https://pay.example/cs_test_1", payment.Url);
        Assert.Equal(50m, payment.Amount);
        Assert.Equal(CatalogDefaults.Currency, payment.Currency);
        Assert.Equal(0m, payment.RefundedAmount);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(PaymentTestData.Now, payment.CreatedAt);
        Assert.Equal(PaymentTestData.Now.AddHours(23), payment.ExpiresAt);
        Assert.Null(payment.ProviderPaymentReference);
        Assert.Null(payment.PaidAt);
    }

    [Fact]
    public void Missing_session_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Payment.Create(PaymentTestData.OrderId, PaymentProvider.Stripe, null!, 50m, PaymentTestData.Now));
    }

    [Theory]
    [InlineData("", "https://pay.example/x")]
    [InlineData(" ", "https://pay.example/x")]
    [InlineData("cs_test_1", "")]
    [InlineData("cs_test_1", " ")]
    public void Blank_session_id_or_url_is_rejected(string id, string url)
    {
        var session = PaymentTestData.Session(id, url);

        Assert.Throws<ArgumentException>(() =>
            Payment.Create(PaymentTestData.OrderId, PaymentProvider.Stripe, session, 50m, PaymentTestData.Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_amount_is_rejected(int amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PaymentTestData.Pending(amount));
    }

    [Fact]
    public void Mark_paid_stores_reference_and_time()
    {
        var payment = PaymentTestData.Pending();

        payment.MarkPaid("pi_test_1", PaymentTestData.Later);

        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Equal("pi_test_1", payment.ProviderPaymentReference);
        Assert.Equal(PaymentTestData.Later, payment.PaidAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Mark_paid_with_blank_reference_is_rejected(string reference)
    {
        var payment = PaymentTestData.Pending();

        Assert.Throws<ArgumentException>(() => payment.MarkPaid(reference, PaymentTestData.Later));
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Theory]
    [InlineData(PaymentStatus.Paid)]
    [InlineData(PaymentStatus.Expired)]
    [InlineData(PaymentStatus.Refunded)]
    public void Mark_paid_is_allowed_only_from_pending(PaymentStatus status)
    {
        var payment = InStatus(status);

        Assert.Throws<InvalidOperationException>(() => payment.MarkPaid("pi_other", PaymentTestData.Later));
        Assert.Equal(status, payment.Status);
    }

    [Fact]
    public void Pending_payment_can_expire()
    {
        var payment = PaymentTestData.Pending();

        payment.MarkExpired();

        Assert.Equal(PaymentStatus.Expired, payment.Status);
    }

    [Theory]
    [InlineData(PaymentStatus.Paid)]
    [InlineData(PaymentStatus.Expired)]
    [InlineData(PaymentStatus.Refunded)]
    public void Mark_expired_is_allowed_only_from_pending(PaymentStatus status)
    {
        var payment = InStatus(status);

        Assert.Throws<InvalidOperationException>(payment.MarkExpired);
        Assert.Equal(status, payment.Status);
    }

    [Fact]
    public void Full_refund_sets_refunded()
    {
        var payment = PaymentTestData.Paid(50m);

        payment.Refund(50m);

        Assert.Equal(50m, payment.RefundedAmount);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
    }

    [Fact]
    public void Partial_refund_keeps_payment_paid()
    {
        var payment = PaymentTestData.Paid(50m);

        payment.Refund(20m);

        Assert.Equal(20m, payment.RefundedAmount);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
    }

    [Fact]
    public void Several_partial_refunds_add_up_and_the_last_one_sets_refunded()
    {
        var payment = PaymentTestData.Paid(50m);

        payment.Refund(20m);
        payment.Refund(10.50m);
        Assert.Equal(PaymentStatus.Paid, payment.Status);

        payment.Refund(19.50m);

        Assert.Equal(50m, payment.RefundedAmount);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
    }

    [Theory]
    [InlineData(50.01)]
    [InlineData(100)]
    public void Refund_above_the_paid_amount_is_rejected(double amount)
    {
        var payment = PaymentTestData.Paid(50m);

        Assert.Throws<ArgumentOutOfRangeException>(() => payment.Refund((decimal)amount));
        Assert.Equal(0m, payment.RefundedAmount);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
    }

    [Fact]
    public void Refund_above_the_remaining_amount_is_rejected()
    {
        var payment = PaymentTestData.Paid(50m);
        payment.Refund(30m);

        Assert.Throws<ArgumentOutOfRangeException>(() => payment.Refund(20.01m));
        Assert.Equal(30m, payment.RefundedAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_refund_is_rejected(int amount)
    {
        var payment = PaymentTestData.Paid();

        Assert.Throws<ArgumentOutOfRangeException>(() => payment.Refund(amount));
        Assert.Equal(0m, payment.RefundedAmount);
    }

    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Expired)]
    [InlineData(PaymentStatus.Refunded)]
    public void Refund_is_allowed_only_from_paid(PaymentStatus status)
    {
        var payment = InStatus(status);
        var refunded = payment.RefundedAmount;

        Assert.Throws<InvalidOperationException>(() => payment.Refund(1m));
        Assert.Equal(refunded, payment.RefundedAmount);
        Assert.Equal(status, payment.Status);
    }

    [Fact]
    public void Mark_refunded_returns_the_whole_amount()
    {
        var payment = PaymentTestData.Paid(50m);

        payment.MarkRefunded();

        Assert.Equal(50m, payment.RefundedAmount);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
    }

    [Fact]
    public void Mark_refunded_returns_only_the_remaining_amount()
    {
        var payment = PaymentTestData.Paid(50m);
        payment.Refund(20m);

        payment.MarkRefunded();

        Assert.Equal(50m, payment.RefundedAmount);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
    }

    [Fact]
    public void Mark_refunded_on_pending_payment_is_rejected()
    {
        var payment = PaymentTestData.Pending();

        Assert.Throws<InvalidOperationException>(payment.MarkRefunded);
    }

    private static Payment InStatus(PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => PaymentTestData.Pending(),
        PaymentStatus.Paid => PaymentTestData.Paid(),
        PaymentStatus.Expired => PaymentTestData.Expired(),
        PaymentStatus.Refunded => PaymentTestData.Refunded(),
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}