namespace Store.Domain;

public sealed class ShippingStrategySetting
{
    public string StrategyKey { get; private set; } = string.Empty;

    public string PublicConfigurationJson { get; private set; } = "{}";

    public string ProtectedSecrets { get; private set; } = string.Empty;

    public bool IsEnabled { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    private ShippingStrategySetting()
    {
    }

    public ShippingStrategySetting(
        string strategyKey,
        string publicConfigurationJson,
        string protectedSecrets,
        bool isEnabled,
        bool isActive,
        DateTime updatedAtUtc)
    {
        StrategyKey = strategyKey;
        PublicConfigurationJson = publicConfigurationJson;
        ProtectedSecrets = protectedSecrets;
        IsEnabled = isEnabled;
        IsActive = isActive;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Update(
        string publicConfigurationJson,
        string protectedSecrets,
        bool isEnabled,
        DateTime updatedAtUtc)
    {
        PublicConfigurationJson = publicConfigurationJson;
        ProtectedSecrets = protectedSecrets;
        IsEnabled = isEnabled;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void SetActive(bool active, DateTime updatedAtUtc)
    {
        IsActive = active;
        UpdatedAtUtc = updatedAtUtc;
    }
}
