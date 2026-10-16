namespace Store.Application;

public sealed record PaymentCaptureView(
    Guid OrderId,
    string OrderState,
    PaymentResultStatus PaymentStatus);
