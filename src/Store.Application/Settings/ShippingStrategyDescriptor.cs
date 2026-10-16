namespace Store.Application;

public sealed record ShippingStrategyDescriptor(
    string Key,
    string Name,
    IReadOnlyList<ConfigurationFieldDescriptor> ConfigurationFields,
    IReadOnlyList<string> SecretFields);
