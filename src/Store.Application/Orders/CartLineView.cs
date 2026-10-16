namespace Store.Application;

public sealed record CartLineView(
    Guid ProductId,
    string Name,
    string Sku,
    int Quantity,
    int Available,
    decimal UnitPrice,
    decimal Subtotal);
