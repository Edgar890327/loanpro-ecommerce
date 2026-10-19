namespace Store.Application;

public sealed record OrderView(
    Guid Id,
    string OrderNumber,
    string State,
    DateTime ReservedUntilUtc,
    DateTime CreatedAtUtc,
    decimal Subtotal,
    decimal Shipping,
    decimal Total,
    string ContactEmail,
    ShippingAddressView ShippingAddress,
    IReadOnlyList<OrderItemView> Items);
