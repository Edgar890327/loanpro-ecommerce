using System.Net.Mail;

namespace Store.Application;

public static class CheckoutValidator
{
    public static IReadOnlyDictionary<string, string[]> Validate(CheckoutInput input)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(input.CustomerName) || input.CustomerName.Trim().Length > 200)
        {
            errors[nameof(input.CustomerName)] = ["Customer name is required and must be at most 200 characters."];
        }

        if (string.IsNullOrWhiteSpace(input.CustomerEmail) ||
            input.CustomerEmail.Trim().Length > 320 ||
            !MailAddress.TryCreate(input.CustomerEmail.Trim(), out _))
        {
            errors[nameof(input.CustomerEmail)] = ["A valid email address of at most 320 characters is required."];
        }

        if (string.IsNullOrWhiteSpace(input.ShippingAddress) || input.ShippingAddress.Trim().Length > 1000)
        {
            errors[nameof(input.ShippingAddress)] =
                ["Shipping address is required and must be at most 1000 characters."];
        }

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
}
