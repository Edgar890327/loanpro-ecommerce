namespace Store.Application;

public sealed record MyOrderItemView(
    string ProductName,
    string Sku,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal);
