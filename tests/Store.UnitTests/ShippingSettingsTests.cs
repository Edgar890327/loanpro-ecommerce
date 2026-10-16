using Microsoft.Extensions.Configuration;
using Store.Application;
using Store.Infrastructure.Orders;

namespace Store.UnitTests;

public sealed class ShippingSettingsTests
{
    [Fact]
    public void FlatRateStrategyUsesConfiguredUsdAmount()
    {
        var strategy = new FlatRateShippingStrategy(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Shipping:FlatRateUsd"] = "3.25" })
            .Build());
        var context = new ShippingContext([new ShippingItem(2, 1.5m)], 40m, "100 Main Street");
        var configuration = new ShippingStrategyConfiguration(
            new Dictionary<string, string> { ["amountUsd"] = "8.75" },
            new Dictionary<string, string>());

        Assert.Equal(8.75m, strategy.CalculateShipping(context, configuration));
        Assert.Equal("flat-rate", strategy.Descriptor.Key);
        Assert.Equal("3.25", strategy.DefaultConfiguration["amountUsd"]);
    }

    [Fact]
    public void ConfigurationSchemaValidatesRequiredValuesAndTypes()
    {
        ConfigurationFieldDescriptor[] fields =
        [
            new("amount", "Amount", "number", true, false, Minimum: 0),
            new("enabled", "Enabled", "boolean", true, false),
            new("token", "Token", "password", true, true)
        ];
        var publicValues = new Dictionary<string, string>
        {
            ["amount"] = "12.50",
            ["enabled"] = "true"
        };

        ConfigurationSchemaValidator.Validate(
            fields,
            ["token"],
            publicValues,
            new Dictionary<string, string> { ["token"] = "protected-value" },
            true);

        Assert.Throws<ArgumentException>(() => ConfigurationSchemaValidator.Validate(
            fields,
            ["token"],
            new Dictionary<string, string> { ["amount"] = "-1", ["enabled"] = "true" },
            new Dictionary<string, string> { ["token"] = "secret" },
            true));
        Assert.Throws<ArgumentException>(() => ConfigurationSchemaValidator.Validate(
            fields,
            ["token"],
            publicValues,
            new Dictionary<string, string>(),
            true));
    }

    [Fact]
    public void SecretMaskingNeverReturnsTheConfiguredSecret()
    {
        var masked = SecretMasker.Mask(["clientSecret"]);

        Assert.Equal("********", masked["clientSecret"]);
        Assert.DoesNotContain("actual-secret", masked.Values);
    }

    [Fact]
    public void ActiveSettingInvariantRequiresExactlyOneActiveSetting()
    {
        ActiveSettingInvariant.EnsureExactlyOne(1);

        Assert.Throws<InvalidOperationException>(() => ActiveSettingInvariant.EnsureExactlyOne(0));
        Assert.Throws<InvalidOperationException>(() => ActiveSettingInvariant.EnsureExactlyOne(2));
    }
}
