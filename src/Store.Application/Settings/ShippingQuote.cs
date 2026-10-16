namespace Store.Application;

public sealed record ShippingQuote(
    string StrategyKey,
    decimal Subtotal,
    decimal Shipping,
    decimal Total);
