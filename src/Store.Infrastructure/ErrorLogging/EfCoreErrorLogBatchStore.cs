using Microsoft.EntityFrameworkCore;
using Store.Domain;

namespace Store.Infrastructure.ErrorLogging;

public sealed class EfCoreErrorLogBatchStore(
    IDbContextFactory<StoreDbContext> contextFactory) : IErrorLogBatchStore
{
    public async Task InsertBatchAsync(
        IReadOnlyCollection<ErrorLog> errorLogs,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        database.ErrorLogs.AddRange(errorLogs);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await database.ErrorLogs
            .Where(errorLog => errorLog.TimestampUtc < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
