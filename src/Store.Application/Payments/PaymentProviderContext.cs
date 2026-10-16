namespace Store.Application;

public sealed record PaymentProviderContext(
    string IdempotencyKey,
    PaymentProviderConfiguration Configuration);
