namespace Store.Application;

public interface IShippingQuoteService
{
    Task<ShippingQuote> QuoteAsync(
        IReadOnlyList<CartItemInput> items,
        string destination,
        CancellationToken cancellationToken);
}
