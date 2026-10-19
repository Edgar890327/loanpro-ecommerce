using Store.Api.Orders;

namespace Store.UnitTests;

public sealed class CheckoutRequestTests
{
    [Fact]
    public void ToInput_UsesTheCustomerFromTheTokenAndNeverFromTheBody()
    {
        var customerId = Guid.NewGuid();
        var request = new CheckoutRequest(
            new ShippingAddressRequest("Buyer", "1 Test Street", null, "Austin", "TX", "73301", "US"),
            [new CartLineRequest(Guid.NewGuid(), 2)]);

        var input = request.ToInput(customerId);

        Assert.Equal(customerId, input.CustomerId);
        Assert.Equal("Austin", input.ShippingAddress.City);
        Assert.Single(input.Items);
    }

    [Fact]
    public void ToInput_ReplacesMissingFieldsWithEmptyValuesSoValidationRejectsThem()
    {
        var input = new CheckoutRequest(null, null).ToInput(Guid.NewGuid());

        Assert.Equal(string.Empty, input.ShippingAddress.Line1);
        Assert.Empty(input.Items);
    }
}
