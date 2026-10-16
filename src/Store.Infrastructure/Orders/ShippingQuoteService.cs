using Microsoft.EntityFrameworkCore;
using Store.Application;

namespace Store.Infrastructure.Orders;

public sealed class ShippingQuoteService(
    IDbContextFactory<StoreDbContext> contextFactory,
    IShippingStrategyRegistry strategies) : IShippingQuoteService
{
    private const decimal MaximumAmount = 9_999_999_999.99m;

    public async Task<ShippingQuote> QuoteAsync(
        IReadOnlyList<CartItemInput> items,
        string destination,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(destination) || destination.Length > 1000)
        {
            throw new ArgumentException("A valid shipping destination is required.");
        }

        var quantities = items
            .GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
        if (quantities.Count == 0 || quantities.Any(pair => pair.Key == Guid.Empty || pair.Value < 1))
        {
            throw new ArgumentException("At least one valid cart item is required.");
        }

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var products = await database.Products
            .AsNoTracking()
            .Where(product => quantities.Keys.Contains(product.Id))
            .Select(product => new { product.Id, product.Price, product.WeightKg })
            .ToListAsync(cancellationToken);
        if (products.Count != quantities.Count)
        {
            throw new KeyNotFoundException("One or more products were not found.");
        }

        var subtotal = products.Sum(product => product.Price * quantities[product.Id]);
        if (subtotal > MaximumAmount)
        {
            throw new ArgumentException("The cart total is outside the supported range.");
        }
        var shippingContext = new ShippingContext(
            products.Select(product => new ShippingItem(quantities[product.Id], product.WeightKg)).ToArray(),
            subtotal,
            destination.Trim());
        var calculation = await strategies.CalculateAsync(shippingContext, cancellationToken);
        if (calculation.Amount < 0 || calculation.Amount > MaximumAmount - subtotal)
        {
            throw new ArgumentException("The shipping quote is outside the supported range.");
        }
        return new ShippingQuote(
            calculation.StrategyKey,
            subtotal,
            calculation.Amount,
            subtotal + calculation.Amount);
    }
}
