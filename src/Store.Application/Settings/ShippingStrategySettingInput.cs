namespace Store.Application;

public sealed record ShippingStrategySettingInput(
    IReadOnlyDictionary<string, string> PublicConfiguration,
    IReadOnlyDictionary<string, string> Secrets,
    bool Enabled,
    bool Activate);
