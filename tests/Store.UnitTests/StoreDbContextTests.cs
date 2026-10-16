using Microsoft.EntityFrameworkCore;
using Store.Domain;
using Store.Infrastructure;

namespace Store.UnitTests;

public sealed class StoreDbContextTests
{
    [Fact]
    public void Model_MapsErrorLogsAndRequiredIndexes()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseNpgsql("Host=localhost;Database=store;Username=store;Password=test")
            .Options;
        using var database = new StoreDbContext(options);

        var entity = database.Model.FindEntityType(typeof(ErrorLog));

        Assert.NotNull(entity);
        Assert.Equal("ErrorLog", entity.GetTableName());
        Assert.Equal("Timestamp", entity.FindProperty(nameof(ErrorLog.TimestampUtc))!.GetColumnName());
        Assert.False(entity.FindProperty(nameof(ErrorLog.Message))!.IsNullable);
        Assert.Contains(entity.GetIndexes(), index =>
            index.Properties.Any(property => property.Name == nameof(ErrorLog.TimestampUtc)));
        Assert.Contains(entity.GetIndexes(), index =>
            index.Properties.Any(property => property.Name == nameof(ErrorLog.CorrelationId)));
    }
}
