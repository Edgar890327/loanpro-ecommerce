using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Store.Infrastructure;

public sealed class StoreDbContextFactory : IDesignTimeDbContextFactory<StoreDbContext>
{
    public StoreDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=store;Username=store;Password=store-local-only";

        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new StoreDbContext(options);
    }
}
