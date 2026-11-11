using EShop.Domain.Payments;

namespace EShop.Domain.UnitTests.Payments;

internal static class PaymentTestData
{
    public static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset Later = Now.AddMinutes(10);
    public static readonly Guid OrderId = Guid.Parse("0196a5c0-0000-7000-8000-000000000002");

    public static HostedPaymentSession Session(string id = "cs_test_1", string url = "https://pay.example/cs_test_1") =>
        new(id, url, Now.AddHours(23));

    public static Payment Pending(decimal amount = 50m) =>
        Payment.Create(OrderId, PaymentProvider.Stripe, Session(), amount, Now);

    public static Payment Paid(decimal amount = 50m)
    {
        var payment = Pending(amount);
        payment.MarkPaid("pi_test_1", Later);
        return payment;
    }

    public static Payment Expired()
    {
        var payment = Pending();
        payment.MarkExpired();
        return payment;
    }

    public static Payment Refunded()
    {
        var payment = Paid();
        payment.Refund(payment.Amount);
        return payment;
    }
}