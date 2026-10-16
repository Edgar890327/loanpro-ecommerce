namespace Store.Application;

public sealed record PaymentServiceResult(
    PaymentOutcome Outcome,
    PaymentCreationView? Creation,
    PaymentCaptureView? Capture);
