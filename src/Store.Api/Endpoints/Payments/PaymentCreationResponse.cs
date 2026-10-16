namespace Store.Api.Payments;

public sealed record PaymentCreationResponse(
    Guid OrderId,
    string ProviderKey,
    string ExternalReference,
    PaymentProviderResponse Provider);
