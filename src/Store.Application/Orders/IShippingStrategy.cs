namespace Store.Application;

public interface IShippingStrategy
{
    ShippingStrategyDescriptor Descriptor { get; }

    IReadOnlyDictionary<string, string> DefaultConfiguration { get; }

    decimal CalculateShipping(
        ShippingContext context,
        ShippingStrategyConfiguration configuration);
}
