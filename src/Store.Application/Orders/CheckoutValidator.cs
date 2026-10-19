namespace Store.Application;

public static class CheckoutValidator
{
    public static IReadOnlyDictionary<string, string[]> Validate(CheckoutInput input)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var address = input.ShippingAddress;
        Require(errors, "RecipientName", address?.RecipientName, 200);
        Require(errors, "Line1", address?.Line1, 200);
        if (address?.Line2 is { } line2 && line2.Trim().Length > 200)
        {
            errors["Line2"] = ["Address line 2 must be at most 200 characters."];
        }

        Require(errors, "City", address?.City, 100);
        Require(errors, "Region", address?.Region, 100);
        Require(errors, "PostalCode", address?.PostalCode, 20);
        Require(errors, "Country", address?.Country, 100);

        if (input.Items.Count is < 1 or > 50)
        {
            errors[nameof(input.Items)] = ["The cart must contain between 1 and 50 items."];
        }
        else if (input.Items.Any(item => item.ProductId == Guid.Empty || item.Quantity < 1))
        {
            errors[nameof(input.Items)] = ["Every cart item requires a product ID and a positive quantity."];
        }

        return errors;
    }

    private static void Require(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maxLength)
        {
            errors[field] = [$"{field} is required and must be at most {maxLength} characters."];
        }
    }
}
