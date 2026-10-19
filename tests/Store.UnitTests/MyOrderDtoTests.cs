using Store.Api.Me;

namespace Store.UnitTests;

public sealed class MyOrderDtoTests
{
    private static readonly string[] Forbidden =
    [
        "IdempotencyKeyHash", "RequestHash", "CustomerId", "Payments", "UpdatedAtUtc", "Status", "Amount"
    ];

    [Theory]
    [InlineData(typeof(MyOrderSummaryResponse))]
    [InlineData(typeof(MyOrderDetailResponse))]
    [InlineData(typeof(MyOrderItemResponse))]
    [InlineData(typeof(MyOrdersPageResponse))]
    public void Responses_DoNotExposeInternalFields(Type type)
    {
        var names = type.GetProperties().Select(property => property.Name).ToArray();

        Assert.Empty(names.Intersect(Forbidden));
    }

    [Fact]
    public void DetailResponse_ExposesOnlyTheProviderAndReferenceOfThePayment()
    {
        var paymentProperties = typeof(MyOrderDetailResponse).GetProperties()
            .Select(property => property.Name)
            .Where(name => name.StartsWith("Payment", StringComparison.Ordinal))
            .Order();

        Assert.Equal(["PaymentProviderKey", "PaymentReference"], paymentProperties);
    }
}
