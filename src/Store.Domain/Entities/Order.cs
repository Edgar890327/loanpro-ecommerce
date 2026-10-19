namespace Store.Domain;

public sealed class Order
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string IdempotencyKeyHash { get; private set; } = string.Empty;

    public string RequestHash { get; private set; } = string.Empty;

    public Guid CustomerId { get; private set; }

    public string OrderNumber { get; private set; } = string.Empty;

    public string ContactEmail { get; private set; } = string.Empty;

    public ShippingAddress ShippingAddress { get; private set; } = null!;

    public decimal Subtotal { get; private set; }

    public decimal Shipping { get; private set; }

    public decimal Total { get; private set; }

    public OrderState State { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ReservedUntilUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public ICollection<Payment> Payments { get; private set; } = new List<Payment>();

    public ICollection<OrderItem> Items { get; private set; } = new List<OrderItem>();

    private Order()
    {
    }

    public Order(
        Guid customerId,
        string orderNumber,
        string idempotencyKeyHash,
        string requestHash,
        string contactEmail,
        ShippingAddress shippingAddress,
        IEnumerable<OrderItem> items,
        decimal shipping,
        DateTime nowUtc,
        DateTime reservedUntilUtc)
    {
        if (shipping < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(shipping));
        }

        CustomerId = customerId;
        OrderNumber = orderNumber;
        IdempotencyKeyHash = idempotencyKeyHash;
        RequestHash = requestHash;
        ContactEmail = contactEmail.Trim();
        ShippingAddress = shippingAddress;
        Items = items.ToList();
        Subtotal = Items.Sum(item => item.Subtotal);
        Shipping = shipping;
        Total = Subtotal + shipping;
        State = OrderState.Reserved;
        CreatedAtUtc = nowUtc;
        ReservedUntilUtc = reservedUntilUtc;
        UpdatedAtUtc = nowUtc;
    }

    public bool Cancel(DateTime nowUtc)
    {
        if (State != OrderState.Reserved)
        {
            return false;
        }

        State = OrderState.Cancelled;
        UpdatedAtUtc = nowUtc;
        return true;
    }

    public bool Fail(DateTime nowUtc)
    {
        if (State != OrderState.Reserved)
        {
            return false;
        }

        State = OrderState.Failed;
        UpdatedAtUtc = nowUtc;
        return true;
    }

    public bool Expire(DateTime nowUtc)
    {
        if (State != OrderState.Reserved || ReservedUntilUtc > nowUtc)
        {
            return false;
        }

        State = OrderState.Expired;
        UpdatedAtUtc = nowUtc;
        return true;
    }

    public bool MarkPaid(DateTime nowUtc)
    {
        if (State != OrderState.Reserved || ReservedUntilUtc <= nowUtc)
        {
            return false;
        }

        State = OrderState.Paid;
        UpdatedAtUtc = nowUtc;
        return true;
    }
}
