namespace Store.Api.Orders;

public sealed record OrderResponse(
    Guid Id,
    string OrderNumber,
    string State,
    DateTimeOffset ReservedUntilUtc,
    DateTimeOffset CreatedAtUtc,
    decimal Subtotal,
    decimal Shipping,
    decimal Total,
    string ContactEmail,
    ShippingAddressResponse ShippingAddress,
    IReadOnlyList<OrderItemResponse> Items)
{
    public static OrderResponse From(Store.Application.OrderView order) => new(
        order.Id,
        order.OrderNumber,
        order.State,
        new DateTimeOffset(DateTime.SpecifyKind(order.ReservedUntilUtc, DateTimeKind.Utc)),
        new DateTimeOffset(DateTime.SpecifyKind(order.CreatedAtUtc, DateTimeKind.Utc)),
        order.Subtotal,
        order.Shipping,
        order.Total,
        order.ContactEmail,
        new ShippingAddressResponse(
            order.ShippingAddress.RecipientName,
            order.ShippingAddress.Line1,
            order.ShippingAddress.Line2,
            order.ShippingAddress.City,
            order.ShippingAddress.Region,
            order.ShippingAddress.PostalCode,
            order.ShippingAddress.Country),
        order.Items.Select(item => new OrderItemResponse(
            item.ProductId,
            item.Name,
            item.Sku,
            item.Quantity,
            item.UnitPrice,
            item.Subtotal)).ToArray());
}
