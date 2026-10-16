namespace Store.Application;

public static class ProductValidator
{
    public const int NameMaxLength = 200;
    public const int SkuMaxLength = 64;
    public const int DescriptionMaxLength = 2000;
    public const int CategoryMaxLength = 100;

    public static IReadOnlyDictionary<string, string[]> Validate(ProductInput input)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            AddError(errors, nameof(input.Name), "Name is required.");
        }
        else if (input.Name.Trim().Length > NameMaxLength)
        {
            AddError(errors, nameof(input.Name), $"Name must be at most {NameMaxLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(input.Sku))
        {
            AddError(errors, nameof(input.Sku), "SKU is required.");
        }
        else if (input.Sku.Trim().Length > SkuMaxLength)
        {
            AddError(errors, nameof(input.Sku), $"SKU must be at most {SkuMaxLength} characters.");
        }

        if (input.Description is { Length: > DescriptionMaxLength })
        {
            AddError(errors, nameof(input.Description),
                $"Description must be at most {DescriptionMaxLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(input.Category))
        {
            AddError(errors, nameof(input.Category), "Category is required.");
        }
        else if (input.Category.Trim().Length > CategoryMaxLength)
        {
            AddError(errors, nameof(input.Category),
                $"Category must be at most {CategoryMaxLength} characters.");
        }

        if (input.Price <= 0 || decimal.Round(input.Price, 2) != input.Price ||
            input.Price > 9_999_999_999.99m)
        {
            AddError(errors, nameof(input.Price), "Price must be greater than zero with at most two decimal places.");
        }

        if (input.Stock < 0)
        {
            AddError(errors, nameof(input.Stock), "Stock cannot be negative.");
        }

        if (input.WeightKg < 0 || input.WeightKg > 999_999_999.999m ||
            input.WeightKg is { } weight && decimal.Round(weight, 3) != weight)
        {
            AddError(errors, nameof(input.WeightKg), "Weight must be non-negative and fit the supported precision.");
        }

        return errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
    }

    private static void AddError(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var messages))
        {
            messages = [];
            errors.Add(field, messages);
        }

        messages.Add(message);
    }
}
