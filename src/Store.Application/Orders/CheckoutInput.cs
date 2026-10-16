namespace Store.Application;

public sealed record CheckoutInput(
    string CustomerName,
    string CustomerEmail,
    string ShippingAddress,
    IReadOnlyList<CartItemInput> Items);
