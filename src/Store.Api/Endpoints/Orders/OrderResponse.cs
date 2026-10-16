namespace Store.Api.Orders;

public sealed record OrderResponse(
    Guid Id,
    string State,
    DateTimeOffset ReservedUntilUtc,
    DateTimeOffset CreatedAtUtc,
    decimal Subtotal,
    decimal Shipping,
    decimal Total,
    IReadOnlyList<OrderItemResponse> Items)
{
    public static OrderResponse From(Store.Application.OrderView order) => new(
        order.Id,
        order.State,
        new DateTimeOffset(DateTime.SpecifyKind(order.ReservedUntilUtc, DateTimeKind.Utc)),
        new DateTimeOffset(DateTime.SpecifyKind(order.CreatedAtUtc, DateTimeKind.Utc)),
        order.Subtotal,
        order.Shipping,
        order.Total,
        order.Items.Select(item => new OrderItemResponse(
            item.ProductId,
            item.Name,
            item.Sku,
            item.Quantity,
            item.UnitPrice,
            item.Subtotal)).ToArray());
}
