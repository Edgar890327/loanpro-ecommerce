using Microsoft.EntityFrameworkCore;
using Store.Domain;
using Store.Infrastructure;

namespace Store.Api.Admin;

public static class ErrorLogEndpoints
{
    public static RouteGroupBuilder MapErrorLogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/error-logs")
            .RequireAuthorization("Admin");
        group.MapGet("/", ListAsync);
        return group;
    }

    private static async Task<IResult> ListAsync(
        StoreDbContext database,
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

        IQueryable<ErrorLog> query = database.ErrorLogs.AsNoTracking();
        if (fromUtc.HasValue)
        {
            query = query.Where(errorLog => errorLog.TimestampUtc >= fromUtc.Value.UtcDateTime);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(errorLog => errorLog.TimestampUtc <= toUtc.Value.UtcDateTime);
        }

        if (!string.IsNullOrWhiteSpace(level))
        {
            query = query.Where(errorLog => errorLog.Level == level.Trim());
        }

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            query = query.Where(errorLog => errorLog.CorrelationId == correlationId.Trim());
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(errorLog => errorLog.TimestampUtc)
            .ThenByDescending(errorLog => errorLog.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return Results.Ok(new ErrorLogPage(items, page, pageSize, totalCount));
    }
}

public sealed record ErrorLogPage(
    IReadOnlyCollection<ErrorLog> Items,
    int Page,
    int PageSize,
    int TotalCount);
