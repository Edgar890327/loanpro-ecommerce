namespace Store.Application;

public interface IPaymentProvider
{
    PaymentProviderDescriptor Descriptor { get; }

    Task<PaymentCreationResult> CreatePaymentAsync(
        PaymentProviderContext context,
        PaymentOrder order,
        CancellationToken cancellationToken);

    Task<PaymentCaptureResult> CapturePaymentAsync(
        PaymentProviderContext context,
        PaymentOrder order,
        string externalReference,
        CancellationToken cancellationToken);
}
