namespace Store.Domain;

public sealed class PaymentProviderSetting
{
    public string ProviderKey { get; private set; } = string.Empty;

    public string PublicConfigurationJson { get; private set; } = "{}";

    public string ProtectedSecrets { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    private PaymentProviderSetting()
    {
    }

    public PaymentProviderSetting(
        string providerKey,
        string publicConfigurationJson,
        string protectedSecrets,
        bool isActive,
        DateTime updatedAtUtc)
    {
        ProviderKey = providerKey;
        PublicConfigurationJson = publicConfigurationJson;
        ProtectedSecrets = protectedSecrets;
        IsActive = isActive;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Update(
        string publicConfigurationJson,
        string protectedSecrets,
        DateTime updatedAtUtc)
    {
        PublicConfigurationJson = publicConfigurationJson;
        ProtectedSecrets = protectedSecrets;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void SetActive(bool active, DateTime updatedAtUtc)
    {
        IsActive = active;
        UpdatedAtUtc = updatedAtUtc;
    }
}
