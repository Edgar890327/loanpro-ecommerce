namespace Store.Api.Shipping;

public sealed record ShippingQuoteRequest(
    IReadOnlyList<Store.Api.Orders.CartLineRequest>? Items,
    string? Destination);
