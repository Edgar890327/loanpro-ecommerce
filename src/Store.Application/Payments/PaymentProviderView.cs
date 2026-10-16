namespace Store.Application;

public sealed record PaymentProviderView(
    PaymentProviderDescriptor Descriptor,
    IReadOnlyDictionary<string, string> PublicConfiguration,
    bool IsActive);
