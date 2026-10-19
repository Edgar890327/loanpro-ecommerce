namespace Store.Api.Orders;

public sealed record CheckoutRequest(
    ShippingAddressRequest? ShippingAddress,
    IReadOnlyList<CartLineRequest>? Items)
{
    public Store.Application.CheckoutInput ToInput(Guid customerId) => new(
        customerId,
        new Store.Application.ShippingAddressInput(
            ShippingAddress?.RecipientName ?? string.Empty,
            ShippingAddress?.Line1 ?? string.Empty,
            ShippingAddress?.Line2,
            ShippingAddress?.City ?? string.Empty,
            ShippingAddress?.Region ?? string.Empty,
            ShippingAddress?.PostalCode ?? string.Empty,
            ShippingAddress?.Country ?? string.Empty),
        (Items ?? [])
            .Select(item => new Store.Application.CartItemInput(item.ProductId, item.Quantity))
            .ToArray());
}
