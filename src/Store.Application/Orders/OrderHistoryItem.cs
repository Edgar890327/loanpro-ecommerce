namespace Store.Application;

public sealed record OrderHistoryItem(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    string CustomerEmail,
    string State,
    DateTime CreatedAtUtc,
    decimal Subtotal,
    decimal Shipping,
    decimal Total,
    string? PaymentProviderKey);
