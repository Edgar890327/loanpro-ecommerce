using Microsoft.EntityFrameworkCore;
using Store.Domain;

namespace Store.Infrastructure;

public sealed class StoreDbContext(DbContextOptions<StoreDbContext> options) : DbContext(options)
{
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.HasPostgresExtension("unaccent");

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");
            entity.HasKey(category => category.Id);
            entity.Property(category => category.Name).HasMaxLength(100).IsRequired();
            entity.Property(category => category.NormalizedName).HasMaxLength(100).IsRequired();
            entity.HasIndex(category => category.NormalizedName).IsUnique();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");
            entity.HasKey(product => product.Id);
            entity.Property(product => product.Name).HasMaxLength(200).IsRequired();
            entity.Property(product => product.Sku).HasMaxLength(64).IsRequired();
            entity.Property(product => product.SkuNormalized).HasMaxLength(64).IsRequired();
            entity.Property(product => product.NameNormalized).HasMaxLength(200).IsRequired();
            entity.Property(product => product.Description).HasMaxLength(2000).IsRequired();
            entity.Property(product => product.SearchText).HasMaxLength(2300).IsRequired();
            entity.Property(product => product.Price).HasPrecision(12, 2).IsRequired();
            entity.Property(product => product.Stock).IsRequired();
            entity.Property(product => product.WeightKg).HasPrecision(12, 3);
            entity.Property(product => product.IsDeleted).IsRequired();
            entity.Property(product => product.CreatedAtUtc).IsRequired();
            entity.Property(product => product.UpdatedAtUtc).IsRequired();
            entity.Property<uint>("xmin")
                .HasField("xmin")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .IsRowVersion();
            entity.HasIndex(product => product.SkuNormalized).IsUnique();
            entity.HasIndex(product => product.CategoryId);
            entity.HasIndex(product => product.SearchText)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");
            entity.HasQueryFilter(product => !product.IsDeleted);
            entity.HasOne(product => product.Category)
                .WithMany(category => category.Products)
                .HasForeignKey(product => product.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

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
