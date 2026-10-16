using Microsoft.EntityFrameworkCore;
using Store.Application;

namespace Store.Infrastructure.ErrorLogging;

public sealed class ErrorLogReader(StoreDbContext database) : IErrorLogReader
{
    public async Task<ErrorLogPage> QueryAsync(ErrorLogQuery filters, CancellationToken cancellationToken)
    {
        var query = database.ErrorLogs.AsNoTracking().AsQueryable();
        if (filters.FromUtc.HasValue)
        {
            query = query.Where(errorLog => errorLog.TimestampUtc >= filters.FromUtc.Value.UtcDateTime);
        }

        if (filters.ToUtc.HasValue)
        {
            query = query.Where(errorLog => errorLog.TimestampUtc <= filters.ToUtc.Value.UtcDateTime);
        }

        if (!string.IsNullOrWhiteSpace(filters.Level))
        {
            query = query.Where(errorLog => errorLog.Level == filters.Level.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filters.CorrelationId))
        {
            query = query.Where(errorLog => errorLog.CorrelationId == filters.CorrelationId.Trim());
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(errorLog => errorLog.TimestampUtc)
            .ThenByDescending(errorLog => errorLog.Id)
            .Skip((filters.Page - 1) * filters.PageSize)
            .Take(filters.PageSize)
            .ToListAsync(cancellationToken);
        return new ErrorLogPage(items, filters.Page, filters.PageSize, totalCount);
    }
}
