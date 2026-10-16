namespace Store.Application;

public sealed record ConfigurationFieldDescriptor(
    string Key,
    string Label,
    string Type,
    bool Required,
    bool Secret,
    IReadOnlyList<string>? Options = null,
    decimal? Minimum = null,
    decimal? Maximum = null);
