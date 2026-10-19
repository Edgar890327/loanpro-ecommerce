namespace Store.Application;

public sealed record MyOrderSummary(
    Guid Id,
    string OrderNumber,
    string State,
    decimal Total,
    DateTime CreatedAtUtc,
    DateTime ReservedUntilUtc,
    string? PaymentProviderKey,
    IReadOnlyList<MyOrderItemView> Items);
