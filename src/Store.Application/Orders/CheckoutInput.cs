namespace Store.Application;

public sealed record CheckoutInput(
    Guid CustomerId,
    ShippingAddressInput ShippingAddress,
    IReadOnlyList<CartItemInput> Items);
