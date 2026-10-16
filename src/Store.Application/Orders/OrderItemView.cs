namespace Store.Application;

public sealed record OrderItemView(
    Guid ProductId,
    string Name,
    string Sku,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal);
