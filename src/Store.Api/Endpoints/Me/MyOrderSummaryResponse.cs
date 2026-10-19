namespace Store.Api.Me;

public sealed record MyOrderSummaryResponse(
    Guid Id,
    string OrderNumber,
    string State,
    decimal Total,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ReservedUntilUtc,
    string? PaymentProviderKey,
    IReadOnlyList<MyOrderItemResponse> Items);
