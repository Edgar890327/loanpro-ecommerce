namespace Store.Application;

public sealed record PaymentOrderLine(
    Guid ProductId,
    string Name,
    string Sku,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal);
