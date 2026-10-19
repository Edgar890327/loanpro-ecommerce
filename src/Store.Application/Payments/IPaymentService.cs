namespace Store.Application;

public interface IPaymentService
{
    Task<PaymentServiceResult> CreateAsync(
        Guid orderId,
        Guid customerId,
        CancellationToken cancellationToken);

    Task<PaymentServiceResult> CaptureAsync(
        Guid orderId,
        Guid customerId,
        string externalReference,
        CancellationToken cancellationToken);
}
