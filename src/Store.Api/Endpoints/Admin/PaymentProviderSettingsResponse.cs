namespace Store.Api.Admin;

public sealed record PaymentProviderSettingsResponse(
    string Key,
    string Name,
    IReadOnlyList<Store.Application.ConfigurationFieldDescriptor> ConfigurationFields,
    IReadOnlyList<string> SecretFields,
    IReadOnlyDictionary<string, string> PublicConfiguration,
    IReadOnlyDictionary<string, string> MaskedSecrets,
    bool IsEnabled,
    bool IsActive);
