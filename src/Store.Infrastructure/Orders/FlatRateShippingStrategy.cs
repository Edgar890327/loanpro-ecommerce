using Microsoft.Extensions.Configuration;
using Store.Application;

namespace Store.Infrastructure.Orders;

public sealed class FlatRateShippingStrategy : IShippingStrategy
{
    private readonly decimal _flatRate;

    public FlatRateShippingStrategy(IConfiguration configuration)
    {
        _flatRate = configuration.GetValue<decimal>("Shipping:FlatRateUsd", 0m);
        if (_flatRate < 0 || decimal.Round(_flatRate, 2) != _flatRate)
        {
            throw new InvalidOperationException("Shipping:FlatRateUsd must be a non-negative USD amount.");
        }
    }

    public decimal CalculateShipping(decimal subtotal) => _flatRate;
}
