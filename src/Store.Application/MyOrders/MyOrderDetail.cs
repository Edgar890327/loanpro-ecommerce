namespace Store.Application;

public sealed record MyOrderDetail(
    Guid Id,
    string OrderNumber,
    string State,
    decimal Subtotal,
    decimal Shipping,
    decimal Total,
    DateTime CreatedAtUtc,
    DateTime ReservedUntilUtc,
    string ContactEmail,
    ShippingAddressView ShippingAddress,
    string? PaymentProviderKey,
    string? PaymentReference,
    IReadOnlyList<MyOrderItemView> Items);
