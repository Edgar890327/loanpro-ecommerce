namespace Store.Application;

public sealed record PaymentProviderView(
    PaymentProviderDescriptor Descriptor,
    IReadOnlyDictionary<string, string> PublicConfiguration,
    IReadOnlyDictionary<string, string> MaskedSecrets,
    bool IsEnabled,
    bool IsActive);
