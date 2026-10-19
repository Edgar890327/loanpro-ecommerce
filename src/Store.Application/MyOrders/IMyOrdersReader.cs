namespace Store.Application;

public interface IMyOrdersReader
{
    Task<MyOrdersPage> ListAsync(MyOrdersFilter filter, CancellationToken cancellationToken);

    Task<MyOrderDetail?> GetAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken);
}
