using Microsoft.EntityFrameworkCore;
using Store.Application;

namespace Store.Infrastructure;

public sealed class DatabaseHealthReader(StoreDbContext database) : IDatabaseHealthReader
{
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) =>
        database.Database.CanConnectAsync(cancellationToken);
}
