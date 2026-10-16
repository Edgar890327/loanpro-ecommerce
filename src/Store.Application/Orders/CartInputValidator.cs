namespace Store.Application;

public static class CartInputValidator
{
    public static IReadOnlyDictionary<string, string[]> Validate(IReadOnlyList<CartItemInput> items)
    {
        if (items.Count is < 1 or > 50)
        {
            return new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["items"] = ["The cart must contain between 1 and 50 items."]
            };
        }

        if (items.Any(item => item.ProductId == Guid.Empty || item.Quantity < 1))
        {
            return new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["items"] = ["Every cart item requires a product ID and a positive quantity."]
            };
        }

        try
        {
            foreach (var group in items.GroupBy(item => item.ProductId))
            {
                _ = group.Aggregate(0, (total, item) => checked(total + item.Quantity));
            }
        }
        catch (OverflowException)
        {
            return new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["items"] = ["Combined item quantities exceed the supported limit."]
            };
        }

        return new Dictionary<string, string[]>(StringComparer.Ordinal);
    }
}
