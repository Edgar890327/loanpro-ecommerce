using Store.Domain;

namespace Store.Infrastructure.ErrorLogging;

public interface IErrorLogBatchStore
{
    Task InsertBatchAsync(IReadOnlyCollection<ErrorLog> errorLogs, CancellationToken cancellationToken);

    Task DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken);
}
