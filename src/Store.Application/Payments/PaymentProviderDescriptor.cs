namespace Store.Application;

public sealed record PaymentProviderDescriptor(
    string Key,
    string Name,
    IReadOnlyList<PaymentConfigurationField> ConfigurationFields,
    IReadOnlyList<string> SecretFields);
