using Store.Domain;

namespace Store.UnitTests;

public sealed class OrderDomainTests
{
    [Fact]
    public void Expire_OnlyTransitionsReservedOrdersAfterTheDeadline()
    {
        var now = DateTime.UtcNow;
        var order = CreateOrder(now, now.AddMinutes(1));

        Assert.False(order.Expire(now));
        Assert.Equal(OrderState.Reserved, order.State);
        Assert.True(order.Expire(now.AddMinutes(1)));
        Assert.Equal(OrderState.Expired, order.State);
        Assert.False(order.Cancel(now.AddMinutes(2)));
    }

    [Fact]
    public void CancelAndFail_OnlyTransitionReservedOrders()
    {
        var now = DateTime.UtcNow;
        var cancelled = CreateOrder(now, now.AddMinutes(15));
        var failed = CreateOrder(now, now.AddMinutes(15));

        Assert.True(cancelled.Cancel(now.AddMinutes(1)));
        Assert.Equal(OrderState.Cancelled, cancelled.State);
        Assert.True(failed.Fail(now.AddMinutes(1)));
        Assert.Equal(OrderState.Failed, failed.State);
    }

    [Fact]
    public void Order_StoresFrozenUnitPriceAndCalculatesTotals()
    {
        var now = DateTime.UtcNow;
        var order = CreateOrder(now, now.AddMinutes(15), shipping: 3.5m);

        Assert.Equal(20m, order.Subtotal);
        Assert.Equal(3.5m, order.Shipping);
        Assert.Equal(23.5m, order.Total);
        Assert.Equal(10m, Assert.Single(order.Items).UnitPrice);
    }

    private static readonly Guid Customer = Guid.NewGuid();

    private static readonly ShippingAddress Address =
        new("Buyer", "1 Test Street", null, "Austin", "TX", "73301", "US");

    [Fact]
    public void Order_FreezesTheCustomerContactAndShippingAddress()
    {
        var now = DateTime.UtcNow;
        var address = new ShippingAddress(" Buyer ", "1 Test Street", null, "Austin", "TX", "73301", "US");
        var order = new Order(
            Customer,
            "ORD-2026-000007",
            new string('A', 64),
            new string('B', 64),
            " buyer@example.test ",
            address,
            [new OrderItem(Guid.NewGuid(), "Tea", "TEA", 1, 5m)],
            0,
            now,
            now.AddMinutes(15));

        Assert.Equal(Customer, order.CustomerId);
        Assert.Equal("buyer@example.test", order.ContactEmail);
        Assert.Equal("ORD-2026-000007", order.OrderNumber);
        Assert.Equal(address, order.ShippingAddress);
    }

    private static Order CreateOrder(DateTime now, DateTime reservedUntil, decimal shipping = 0) =>
        new(
            Customer,
            "ORD-2026-000001",
            new string('A', 64),
            new string('B', 64),
            "buyer@example.test",
            Address,
            [new OrderItem(Guid.NewGuid(), "Tea", "TEA", 2, 10m)],
            shipping,
            now,
            reservedUntil);
}
