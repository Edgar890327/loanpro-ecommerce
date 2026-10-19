using System.Security.Claims;
using Store.Api.Auth;
using Store.Api.Orders;
using Store.Application;

namespace Store.Api.Me;

public static class MyOrdersEndpoints
{
    public static void MapMyOrdersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/me/orders", ListAsync)
            .RequireAuthorization(AuthorizationPolicies.CustomerOnly)
            .WithName("GetMyOrders")
            .WithSummary("Search the signed-in customer's purchases")
            .Produces<MyOrdersPageResponse>()
            .ProducesValidationProblem();

        endpoints.MapGet("/me/orders/{id:guid}", GetAsync)
            .RequireAuthorization(AuthorizationPolicies.CustomerOnly)
            .WithName("GetMyOrder")
            .WithSummary("Get one of the signed-in customer's purchases")
            .Produces<MyOrderDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal user,
        IMyOrdersReader reader,
        string? q,
        string? status,
        string? from,
        string? to,
        string? sort,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = MyOrdersFilterBuilder.DefaultPageSize)
    {
        if (user.GetUserId() is not { } customerId)
        {
            return Results.Unauthorized();
        }

        var built = MyOrdersFilterBuilder.Build(customerId, new MyOrdersQuery(q, status, from, to, sort, page, pageSize));
        if (built.Filter is null)
        {
            return Results.ValidationProblem(built.Errors.ToDictionary(pair => pair.Key, pair => pair.Value));
        }

        var result = await reader.ListAsync(built.Filter, cancellationToken);
        return Results.Ok(new MyOrdersPageResponse(
            result.Items.Select(ToResponse).ToArray(),
            result.Page,
            result.PageSize,
            result.TotalCount));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        ClaimsPrincipal user,
        IMyOrdersReader reader,
        CancellationToken cancellationToken)
    {
        if (user.GetUserId() is not { } customerId)
        {
            return Results.Unauthorized();
        }

        var order = await reader.GetAsync(customerId, id, cancellationToken);
        return order is null ? Results.NotFound() : Results.Ok(ToResponse(order));
    }

    public static MyOrderSummaryResponse ToResponse(MyOrderSummary order) => new(
        order.Id,
        order.OrderNumber,
        order.State,
        order.Total,
        ToUtc(order.CreatedAtUtc),
        ToUtc(order.ReservedUntilUtc),
        order.PaymentProviderKey,
        order.Items.Select(ToResponse).ToArray());

    public static MyOrderDetailResponse ToResponse(MyOrderDetail order) => new(
        order.Id,
        order.OrderNumber,
        order.State,
        order.Subtotal,
        order.Shipping,
        order.Total,
        ToUtc(order.CreatedAtUtc),
        ToUtc(order.ReservedUntilUtc),
        order.ContactEmail,
        new ShippingAddressResponse(
            order.ShippingAddress.RecipientName,
            order.ShippingAddress.Line1,
            order.ShippingAddress.Line2,
            order.ShippingAddress.City,
            order.ShippingAddress.Region,
            order.ShippingAddress.PostalCode,
            order.ShippingAddress.Country),
        order.PaymentProviderKey,
        order.PaymentReference,
        order.Items.Select(ToResponse).ToArray());

    private static MyOrderItemResponse ToResponse(MyOrderItemView item) =>
        new(item.ProductName, item.Sku, item.Quantity, item.UnitPrice, item.Subtotal);

    private static DateTimeOffset ToUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
