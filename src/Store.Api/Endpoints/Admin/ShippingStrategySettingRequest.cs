namespace Store.Api.Admin;

public sealed record ShippingStrategySettingRequest(
    IReadOnlyDictionary<string, string>? PublicConfiguration,
    IReadOnlyDictionary<string, string>? Secrets,
    bool Enabled,
    bool Activate);
