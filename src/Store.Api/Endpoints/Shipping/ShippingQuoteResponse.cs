namespace Store.Api.Shipping;

public sealed record ShippingQuoteResponse(
    string StrategyKey,
    decimal Subtotal,
    decimal Shipping,
    decimal Total);
