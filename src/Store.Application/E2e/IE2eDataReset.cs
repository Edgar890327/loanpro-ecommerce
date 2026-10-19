namespace Store.Application;

public interface IE2eDataReset
{
    Task ResetAsync(CancellationToken cancellationToken);
}
