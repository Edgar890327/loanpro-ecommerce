namespace Store.Api.Admin;

public sealed record OrderHistoryResponse(
    Guid Id,
    string State,
    DateTimeOffset CreatedAtUtc,
    decimal Subtotal,
    decimal Shipping,
    decimal Total,
    string? PaymentProviderKey);
