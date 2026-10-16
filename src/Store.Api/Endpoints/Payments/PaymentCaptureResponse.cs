namespace Store.Api.Payments;

public sealed record PaymentCaptureResponse(
    Guid OrderId,
    string OrderState,
    string PaymentStatus);
