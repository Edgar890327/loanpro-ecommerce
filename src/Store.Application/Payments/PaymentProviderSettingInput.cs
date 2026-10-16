namespace Store.Application;

public sealed record PaymentProviderSettingInput(
    IReadOnlyDictionary<string, string> PublicConfiguration,
    IReadOnlyDictionary<string, string> Secrets,
    bool Activate,
    bool Enabled = true);
