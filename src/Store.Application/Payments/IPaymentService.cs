namespace Store.Application;

public interface IPaymentService
{
    Task<PaymentServiceResult> CreateAsync(
        Guid orderId,
        string orderCapability,
        CancellationToken cancellationToken);

    Task<PaymentServiceResult> CaptureAsync(
        Guid orderId,
        string orderCapability,
        string externalReference,
        CancellationToken cancellationToken);
}
