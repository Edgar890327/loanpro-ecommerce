using Microsoft.Extensions.Configuration;
using Store.Application;

namespace Store.Infrastructure.Orders;

public sealed class FlatRateShippingStrategy : IShippingStrategy
{
    public const string StrategyKey = "flat-rate";
    private readonly string _defaultAmount;

    public FlatRateShippingStrategy(IConfiguration configuration)
    {
        var amount = configuration.GetValue<decimal>("Shipping:FlatRateUsd", 0m);
        if (amount < 0 || decimal.Round(amount, 2) != amount)
        {
            throw new InvalidOperationException("Shipping:FlatRateUsd must be a non-negative USD amount.");
        }

        _defaultAmount = amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public ShippingStrategyDescriptor Descriptor { get; } = new(
        StrategyKey,
        "Flat rate",
        [
            new ConfigurationFieldDescriptor(
                "amountUsd",
                "Flat shipping amount (USD)",
                "number",
                true,
                false,
                Minimum: 0,
                Maximum: 9_999_999_999.99m)
        ],
        []);

    public IReadOnlyDictionary<string, string> DefaultConfiguration =>
        new Dictionary<string, string>(StringComparer.Ordinal) { ["amountUsd"] = _defaultAmount };

    public decimal CalculateShipping(
        ShippingContext context,
        ShippingStrategyConfiguration configuration)
    {
        if (!configuration.PublicValues.TryGetValue("amountUsd", out var amountText) ||
            !decimal.TryParse(
                amountText,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var amount) ||
            amount < 0 ||
                amount > 9_999_999_999.99m ||
                decimal.Round(amount, 2) != amount)
        {
            throw new InvalidOperationException("Flat shipping configuration must be a non-negative USD amount.");
        }

        return amount;
    }
}
