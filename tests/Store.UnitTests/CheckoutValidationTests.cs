using Store.Application;

namespace Store.UnitTests;

public sealed class CheckoutValidationTests
{
    [Fact]
    public void CheckoutValidator_RejectsInvalidGuestDetailsAndQuantities()
    {
        var input = new CheckoutInput(
            " ",
            "not-an-email",
            new string('x', 1001),
            [new CartItemInput(Guid.Empty, 0)]);

        var errors = CheckoutValidator.Validate(input);

        Assert.Contains(nameof(input.CustomerName), errors.Keys);
        Assert.Contains(nameof(input.CustomerEmail), errors.Keys);
        Assert.Contains(nameof(input.ShippingAddress), errors.Keys);
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
    public void CheckoutValidator_AcceptsValidGuestDetails()
    {
        var input = new CheckoutInput(
            "Guest Buyer",
            "buyer@example.test",
            "100 Test Street",
            [new CartItemInput(Guid.NewGuid(), 2)]);

        Assert.Empty(CheckoutValidator.Validate(input));
    }
}
