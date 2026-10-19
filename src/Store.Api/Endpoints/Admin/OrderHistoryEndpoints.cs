using Store.Application;

namespace Store.Api.Admin;

public static class OrderHistoryEndpoints
{
    public static void MapOrderHistoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/admin/orders", ListAsync)
            .RequireAuthorization(Store.Api.Auth.AuthorizationPolicies.AdminOnly)
            .WithName("GetAdminOrderHistory")
            .WithSummary("List orders for store administration")
            .Produces<OrderHistoryPageResponse>()
            .ProducesValidationProblem();
    }

    private static async Task<IResult> ListAsync(
        IOrderHistoryReader reader,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["pagination"] = ["Page must be positive and pageSize must be between 1 and 100."]
            });
        }

        var result = await reader.QueryAsync(page, pageSize, cancellationToken);
        return Results.Ok(new OrderHistoryPageResponse(
            result.Items.Select(item => new OrderHistoryResponse(
                item.Id,
                item.State,
                new DateTimeOffset(DateTime.SpecifyKind(item.CreatedAtUtc, DateTimeKind.Utc)),
                item.Subtotal,
                item.Shipping,
                item.Total,
                item.PaymentProviderKey)).ToArray(),
            result.Page,
            result.PageSize,
            result.TotalCount));
    }
}
