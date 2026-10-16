namespace Store.Application;

public interface IErrorLogReader
{
    Task<ErrorLogPage> QueryAsync(ErrorLogQuery query, CancellationToken cancellationToken);
}
