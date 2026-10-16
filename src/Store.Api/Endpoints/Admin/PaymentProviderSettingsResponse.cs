namespace Store.Api.Admin;

public sealed record PaymentProviderSettingsResponse(
    string Key,
    string Name,
    IReadOnlyList<Store.Application.PaymentConfigurationField> ConfigurationFields,
    IReadOnlyList<string> SecretFields,
    IReadOnlyDictionary<string, string> PublicConfiguration,
    bool IsActive);
