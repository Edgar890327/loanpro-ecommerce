namespace Store.Application;

public sealed record PaymentOrder(
    Guid Id,
    decimal Subtotal,
    decimal Shipping,
    decimal Total,
    DateTime ReservedUntilUtc,
    IReadOnlyList<PaymentOrderLine> Items);
