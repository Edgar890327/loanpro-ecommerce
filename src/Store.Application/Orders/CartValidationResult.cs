namespace Store.Application;

public sealed record CartValidationResult(
    bool IsAvailable,
    IReadOnlyList<CartLineView> Items,
    IReadOnlyList<StockIssue> Issues,
    decimal Subtotal,
    decimal Shipping,
    decimal Total);
