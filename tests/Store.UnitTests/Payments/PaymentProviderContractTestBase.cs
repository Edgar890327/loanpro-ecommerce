using Store.Application;

namespace Store.UnitTests.Payments;

public abstract class PaymentProviderContractTestBase
{
    protected abstract IPaymentProvider CreateProvider();

    protected abstract PaymentProviderConfiguration Configuration { get; }

    protected virtual string OrderCapability => "order-capability-1234567890";

    protected static PaymentOrder CreateOrder() => new(
        Guid.Parse("09d185b4-1f51-43e5-a57c-efcdf832ad47"),
        25m,
        2m,
        27m,
        DateTime.UtcNow.AddMinutes(15),
        [
            new PaymentOrderLine(
                Guid.Parse("72d2dffb-5617-44ec-bf42-0b85cf38a283"),
                "Test product",
                "TEST-1",
                1,
                25m,
                25m)
        ]);

    [Fact]
    public async Task CreatesPaymentAndReturnsApprovedUsdCapture()
    {
        var provider = CreateProvider();
        var order = CreateOrder();
        var context = new PaymentProviderContext(OrderCapability, Configuration);

        var creation = await provider.CreatePaymentAsync(context, order, CancellationToken.None);
        var capture = await provider.CapturePaymentAsync(
            context,
            order,
            creation.ExternalReference,
            CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(creation.ExternalReference));
        Assert.Equal(PaymentResultStatus.Approved, capture.Status);
        Assert.Equal(order.Total, capture.Amount);
        Assert.Equal("USD", capture.Currency);
    }
}
