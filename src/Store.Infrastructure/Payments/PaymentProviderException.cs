namespace Store.Infrastructure.Payments;

public sealed class PaymentProviderException(string message, Exception? innerException = null)
    : Exception(message, innerException);
