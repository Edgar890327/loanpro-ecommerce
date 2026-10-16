namespace Store.Application;

public interface IOrderHistoryReader
{
    Task<OrderHistoryPage> QueryAsync(int page, int pageSize, CancellationToken cancellationToken);
}
