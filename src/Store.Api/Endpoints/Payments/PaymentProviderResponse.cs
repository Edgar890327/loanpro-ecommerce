namespace Store.Api.Payments;

public sealed record PaymentProviderResponse(
    string Key,
    string Name,
    IReadOnlyList<Store.Application.PaymentConfigurationField> ConfigurationFields,
    IReadOnlyList<string> SecretFields,
    IReadOnlyDictionary<string, string> PublicConfiguration);
