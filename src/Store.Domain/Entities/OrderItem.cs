namespace Store.Domain;

public sealed class OrderItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid OrderId { get; private set; }

    public Order Order { get; private set; } = null!;

    public Guid ProductId { get; private set; }

    public Product Product { get; private set; } = null!;

    public string ProductName { get; private set; } = string.Empty;

    public string ProductSku { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal Subtotal => UnitPrice * Quantity;

    private OrderItem()
    {
    }

    public OrderItem(Guid productId, string productName, string productSku, int quantity, decimal unitPrice)
    {
        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        ProductId = productId;
        ProductName = productName;
        ProductSku = productSku;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
