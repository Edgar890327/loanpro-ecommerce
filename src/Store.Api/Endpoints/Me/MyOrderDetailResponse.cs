using Store.Api.Orders;

namespace Store.Api.Me;

public sealed record MyOrderDetailResponse(
    Guid Id,
    string OrderNumber,
    string State,
    decimal Subtotal,
    decimal Shipping,
    decimal Total,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ReservedUntilUtc,
    string ContactEmail,
    ShippingAddressResponse ShippingAddress,
    string? PaymentProviderKey,
    string? PaymentReference,
    IReadOnlyList<MyOrderItemResponse> Items);
