namespace Store.Api.Admin;

public sealed record PaymentProviderSettingRequest(
    IReadOnlyDictionary<string, string>? PublicConfiguration,
    IReadOnlyDictionary<string, string>? Secrets,
    bool Activate,
    bool Enabled = true);
