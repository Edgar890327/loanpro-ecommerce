namespace Store.Api.Orders;

public sealed record OrderItemResponse(
    Guid ProductId,
    string Name,
    string Sku,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal);
