namespace Store.Application;

public sealed record PaymentProviderDescriptor(
    string Key,
    string Name,
    IReadOnlyList<ConfigurationFieldDescriptor> ConfigurationFields,
    IReadOnlyList<string> SecretFields);
