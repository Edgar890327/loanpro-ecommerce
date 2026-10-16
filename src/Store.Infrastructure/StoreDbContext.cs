using Microsoft.EntityFrameworkCore;
using Store.Domain;

namespace Store.Infrastructure;

public sealed class StoreDbContext(DbContextOptions<StoreDbContext> options) : DbContext(options)
{
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ErrorLog>(entity =>
        {
            entity.ToTable("ErrorLog");
            entity.HasKey(errorLog => errorLog.Id);
            entity.Property(errorLog => errorLog.TimestampUtc).HasColumnName("Timestamp").IsRequired();
            entity.Property(errorLog => errorLog.Level).IsRequired();
            entity.Property(errorLog => errorLog.CorrelationId).IsRequired();
            entity.Property(errorLog => errorLog.Message).IsRequired();
            entity.Property(errorLog => errorLog.ExceptionType).IsRequired();
            entity.Property(errorLog => errorLog.StackTrace);
            entity.Property(errorLog => errorLog.HttpMethod).IsRequired();
            entity.Property(errorLog => errorLog.Path).IsRequired();
            entity.Property(errorLog => errorLog.StatusCode).IsRequired();
            entity.Property(errorLog => errorLog.OrderId);
            entity.Property(errorLog => errorLog.PaymentProviderKey);
            entity.HasIndex(errorLog => errorLog.TimestampUtc);
            entity.HasIndex(errorLog => errorLog.CorrelationId);
        });
    }
}
