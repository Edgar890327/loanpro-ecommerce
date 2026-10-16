namespace Store.Application;

public sealed record OrderView(
    Guid Id,
    string State,
    DateTime ReservedUntilUtc,
    DateTime CreatedAtUtc,
    decimal Subtotal,
    decimal Shipping,
    decimal Total,
    IReadOnlyList<OrderItemView> Items);
