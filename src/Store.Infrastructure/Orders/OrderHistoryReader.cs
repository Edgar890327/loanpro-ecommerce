using Microsoft.EntityFrameworkCore;
using Store.Application;

namespace Store.Infrastructure.Orders;

public sealed class OrderHistoryReader(IDbContextFactory<StoreDbContext> contextFactory) : IOrderHistoryReader
{
    public async Task<OrderHistoryPage> QueryAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = database.Orders.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);
        var orders = await query
            .OrderByDescending(order => order.CreatedAtUtc)
            .ThenByDescending(order => order.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(order => new OrderHistoryItem(
                order.Id,
                order.State.ToString(),
                order.CreatedAtUtc,
                order.Subtotal,
                order.Shipping,
                order.Total,
                order.Payments.Select(payment => payment.ProviderKey).FirstOrDefault()))
            .ToListAsync(cancellationToken);
        return new OrderHistoryPage(orders, page, pageSize, totalCount);
    }
}
