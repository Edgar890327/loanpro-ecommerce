namespace Store.Application;

public sealed record ShippingContext(
    IReadOnlyList<ShippingItem> Items,
    decimal Subtotal,
    string Destination);
