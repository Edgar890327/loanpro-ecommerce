using Store.Domain;

namespace Store.UnitTests.Payments;

public sealed class PaymentDomainTests
{
    [Fact]
    public void PaymentStoresProviderReferenceStatusAndFrozenAmount()
    {
        var now = DateTime.UtcNow;
        var orderId = Guid.NewGuid();
        var payment = new Payment(orderId, "simulated", "external-1", "Created", 27m, now);

        Assert.NotEqual(Guid.Empty, payment.Id);
        Assert.Equal(orderId, payment.OrderId);
        Assert.Equal("simulated", payment.ProviderKey);
        Assert.Equal("external-1", payment.ExternalReference);
        Assert.Equal("Created", payment.Status);
        Assert.Equal(27m, payment.Amount);
        Assert.Equal("USD", payment.Currency);
        Assert.Equal(now, payment.CreatedAtUtc);
        Assert.Equal(now, payment.UpdatedAtUtc);

        var updated = now.AddMinutes(1);
        payment.Update("external-1", "Approved", updated);

        Assert.Equal("Approved", payment.Status);
        Assert.Equal(updated, payment.UpdatedAtUtc);
    }

    [Fact]
    public void ProviderSettingsCanUpdateConfigurationAndActivation()
    {
        var now = DateTime.UtcNow;
        var setting = new PaymentProviderSetting("paypal", "{}", "protected", false, now);

        Assert.Equal("paypal", setting.ProviderKey);
        Assert.Equal("{}", setting.PublicConfigurationJson);
        Assert.Equal("protected", setting.ProtectedSecrets);
        Assert.False(setting.IsActive);
        Assert.Equal(now, setting.UpdatedAtUtc);

        var updated = now.AddMinutes(1);
        setting.Update("{\"sandbox\":\"true\"}", "new-protected", updated);
        setting.SetActive(true, updated);

        Assert.Equal("{\"sandbox\":\"true\"}", setting.PublicConfigurationJson);
        Assert.Equal("new-protected", setting.ProtectedSecrets);
        Assert.True(setting.IsActive);
        Assert.Equal(updated, setting.UpdatedAtUtc);
    }

    [Fact]
    public void DataProtectionKeyStoresThePersistedXmlDocument()
    {
        var key = new DataProtectionKey("key-name", "<key />");

        Assert.NotEqual(Guid.Empty, key.Id);
        Assert.Equal("key-name", key.FriendlyName);
        Assert.Equal("<key />", key.Xml);
    }
}
