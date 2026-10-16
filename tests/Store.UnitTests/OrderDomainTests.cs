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

    private static Order CreateOrder(DateTime now, DateTime reservedUntil, decimal shipping = 0) =>
        new(
            new string('A', 64),
            new string('B', 64),
            "Guest",
            "guest@example.test",
            "Test address",
            [new OrderItem(Guid.NewGuid(), "Tea", "TEA", 2, 10m)],
            shipping,
            now,
            reservedUntil);
}
