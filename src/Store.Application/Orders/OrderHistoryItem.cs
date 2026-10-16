namespace Store.Application;

public sealed record OrderHistoryItem(
    Guid Id,
    string State,
    DateTime CreatedAtUtc,
    decimal Subtotal,
    decimal Shipping,
    decimal Total,
    string? PaymentProviderKey);
