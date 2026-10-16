namespace Store.Application;

public interface IShippingStrategy
{
    decimal CalculateShipping(decimal subtotal);
}
