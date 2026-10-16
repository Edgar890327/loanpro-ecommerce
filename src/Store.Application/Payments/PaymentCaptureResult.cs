namespace Store.Application;

public sealed record PaymentCaptureResult(
    PaymentResultStatus Status,
    decimal Amount,
    string Currency,
    bool AlreadyCaptured);
