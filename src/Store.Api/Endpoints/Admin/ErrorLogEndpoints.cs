using Store.Application;

namespace Store.Api.Admin;

public static class ErrorLogEndpoints
{
    public static RouteGroupBuilder MapErrorLogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/error-logs")
            .RequireAuthorization(Store.Api.Auth.AuthorizationPolicies.AdminOnly);
        group.MapGet("/", ListAsync);
        return group;
    }

    private static async Task<IResult> ListAsync(
        IErrorLogReader errorLogs,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        string? level = null,
        string? correlationId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100 ||
            (long)(page - 1) * pageSize > int.MaxValue || fromUtc > toUtc)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["filters"] = ["Provide a valid date range, a positive page, and a pageSize between 1 and 100."]
            });
        }

        var filters = new ErrorLogQuery(fromUtc, toUtc, level, correlationId, page, pageSize);
        return Results.Ok(await errorLogs.QueryAsync(filters, cancellationToken));
    }
}
