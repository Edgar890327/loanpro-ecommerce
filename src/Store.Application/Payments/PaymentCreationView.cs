namespace Store.Application;

public sealed record PaymentCreationView(
    Guid OrderId,
    string ProviderKey,
    string ExternalReference,
    PaymentProviderDescriptor Descriptor,
    IReadOnlyDictionary<string, string> PublicConfiguration);
