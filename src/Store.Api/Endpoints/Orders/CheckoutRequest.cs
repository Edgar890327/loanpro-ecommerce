namespace Store.Api.Orders;

public sealed record CheckoutRequest(
    string CustomerName,
    string CustomerEmail,
    string ShippingAddress,
    IReadOnlyList<CartLineRequest>? Items)
{
    public Store.Application.CheckoutInput ToInput() => new(
        CustomerName,
        CustomerEmail,
        ShippingAddress,
        (Items ?? [])
            .Select(item => new Store.Application.CartItemInput(item.ProductId, item.Quantity))
            .ToArray());
}
