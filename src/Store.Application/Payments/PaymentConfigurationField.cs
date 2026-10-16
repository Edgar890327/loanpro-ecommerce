namespace Store.Application;

public sealed record PaymentConfigurationField(
    string Key,
    string Label,
    string Type,
    bool Required,
    bool Secret,
    IReadOnlyList<string>? Options = null);
