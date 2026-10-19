namespace Store.Api.Admin;

public sealed record OrderHistoryResponse(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    string CustomerEmail,
    string State,
    DateTimeOffset CreatedAtUtc,
    decimal Subtotal,
    decimal Shipping,
    decimal Total,
    string? PaymentProviderKey);
