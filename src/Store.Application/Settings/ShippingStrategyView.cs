namespace Store.Application;

public sealed record ShippingStrategyView(
    ShippingStrategyDescriptor Descriptor,
    IReadOnlyDictionary<string, string> PublicConfiguration,
    IReadOnlyDictionary<string, string> MaskedSecrets,
    bool IsEnabled,
    bool IsActive);
