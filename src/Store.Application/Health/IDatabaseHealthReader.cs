namespace Store.Application;

public interface IDatabaseHealthReader
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
}
