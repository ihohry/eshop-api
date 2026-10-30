namespace EShop.Domain.Payments;

public sealed class PaymentGatewayException(string message, Exception? innerException = null)
    : Exception(message, innerException);