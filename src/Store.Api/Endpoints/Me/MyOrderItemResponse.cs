namespace Store.Api.Me;

public sealed record MyOrderItemResponse(
    string ProductName,
    string Sku,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal);
