using Store.Application;

namespace Store.UnitTests;

public sealed class CheckoutValidationTests
{
    [Fact]
    public void CheckoutValidator_RejectsMissingAddressFieldsAndInvalidItems()
    {
        var input = new CheckoutInput(
            Guid.NewGuid(),
            new ShippingAddressInput(" ", new string('x', 201), new string('y', 201), "", "", "", ""),
            [new CartItemInput(Guid.Empty, 0)]);

        var errors = CheckoutValidator.Validate(input);

        Assert.Contains("RecipientName", errors.Keys);
        Assert.Contains("Line1", errors.Keys);
        Assert.Contains("Line2", errors.Keys);
        Assert.Contains("City", errors.Keys);
        Assert.Contains("Region", errors.Keys);
        Assert.Contains("PostalCode", errors.Keys);
        Assert.Contains("Country", errors.Keys);
        Assert.Contains(nameof(input.Items), errors.Keys);
    }

    [Fact]
    public void CartInputValidator_RejectsCombinedQuantityOverflow()
    {
        var productId = Guid.NewGuid();
        var items = new[]
        {
            new CartItemInput(productId, int.MaxValue),
            new CartItemInput(productId, 1)
        };

        var errors = CartInputValidator.Validate(items);

        Assert.Contains("items", errors.Keys);
    }

    [Fact]
    public void CartInputValidator_AcceptsAValidCart()
    {
        var items = new[] { new CartItemInput(Guid.NewGuid(), 2) };

        Assert.Empty(CartInputValidator.Validate(items));
    }

    [Fact]
    public void CheckoutValidator_AcceptsAValidAddressWithoutLineTwo()
    {
        var input = new CheckoutInput(
            Guid.NewGuid(),
            new ShippingAddressInput("Buyer", "100 Test Street", null, "Austin", "TX", "73301", "US"),
            [new CartItemInput(Guid.NewGuid(), 2)]);

        Assert.Empty(CheckoutValidator.Validate(input));
    }
}
