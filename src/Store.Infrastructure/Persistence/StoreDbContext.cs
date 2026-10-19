using Microsoft.EntityFrameworkCore;
using Store.Domain;

namespace Store.Infrastructure;

public sealed class StoreDbContext(DbContextOptions<StoreDbContext> options) : DbContext(options)
{
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<PaymentProviderSetting> PaymentProviderSettings => Set<PaymentProviderSetting>();

    public DbSet<ShippingStrategySetting> ShippingStrategySettings => Set<ShippingStrategySetting>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.HasPostgresExtension("unaccent");

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Email).HasMaxLength(320).IsRequired();
            entity.Property(user => user.EmailNormalized).HasMaxLength(320).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(user => user.FullName).HasMaxLength(200).IsRequired();
            entity.Property(user => user.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(user => user.PreferredLocale).HasMaxLength(10);
            entity.Property(user => user.IsActive).IsRequired();
            entity.Property(user => user.CreatedAtUtc).IsRequired();
            entity.HasIndex(user => user.EmailNormalized).IsUnique();
        });

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
            entity.ToTable("Products", table => table.HasCheckConstraint(
                "CK_Products_Stock_Reserved",
                "\"Stock\" >= \"Reserved\" AND \"Reserved\" >= 0"));
            entity.HasKey(product => product.Id);
            entity.Property(product => product.Name).HasMaxLength(200).IsRequired();
            entity.Property(product => product.Sku).HasMaxLength(64).IsRequired();
            entity.Property(product => product.SkuNormalized).HasMaxLength(64).IsRequired();
            entity.Property(product => product.NameNormalized).HasMaxLength(200).IsRequired();
            entity.Property(product => product.Description).HasMaxLength(2000).IsRequired();
            entity.Property(product => product.SearchText).HasMaxLength(2300).IsRequired();
            entity.Property(product => product.Price).HasPrecision(12, 2).IsRequired();
            entity.Property(product => product.Stock).IsRequired();
            entity.Property(product => product.Reserved).HasDefaultValue(0).IsRequired();
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

        modelBuilder.Entity<ImportBatch>(entity =>
        {
            entity.ToTable("ImportBatches");
            entity.HasKey(batch => batch.Id);
            entity.Property(batch => batch.FileHash).HasMaxLength(64).IsRequired();
            entity.Property(batch => batch.CreatedAtUtc).IsRequired();
            entity.Property(batch => batch.RowCount).IsRequired();
            entity.Property(batch => batch.CreatedProducts).IsRequired();
            entity.Property(batch => batch.StockAdded).IsRequired();
            entity.Property(batch => batch.WarningCount).IsRequired();
            entity.Property(batch => batch.RejectedCount).IsRequired();
            entity.Property(batch => batch.ExactDuplicateCount).IsRequired();
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(batch => batch.ImportedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(batch => batch.FileHash);
            entity.HasIndex(batch => batch.CreatedAtUtc);
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.ToTable("StockMovements");
            entity.HasKey(movement => movement.Id);
            entity.Property(movement => movement.Type).HasMaxLength(32).IsRequired();
            entity.Property(movement => movement.Quantity).IsRequired();
            entity.Property(movement => movement.CreatedAtUtc).IsRequired();
            entity.HasIndex(movement => movement.ProductId);
            entity.HasIndex(movement => movement.ImportBatchId);
            entity.HasIndex(movement => movement.OrderId);
            entity.HasOne(movement => movement.Product)
                .WithMany()
                .HasForeignKey(movement => movement.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(movement => movement.ImportBatch)
                .WithMany(batch => batch.StockMovements)
                .HasForeignKey(movement => movement.ImportBatchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(movement => movement.Order)
                .WithMany()
                .HasForeignKey(movement => movement.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(order => order.Id);
            entity.Property(order => order.IdempotencyKeyHash).HasMaxLength(64).IsRequired();
            entity.Property(order => order.RequestHash).HasMaxLength(64).IsRequired();
            entity.Property(order => order.CustomerName).HasMaxLength(200).IsRequired();
            entity.Property(order => order.CustomerEmail).HasMaxLength(320).IsRequired();
            entity.Property(order => order.ShippingAddress).HasMaxLength(1000).IsRequired();
            entity.Property(order => order.Subtotal).HasPrecision(12, 2).IsRequired();
            entity.Property(order => order.Shipping).HasPrecision(12, 2).IsRequired();
            entity.Property(order => order.Total).HasPrecision(12, 2).IsRequired();
            entity.Property(order => order.State).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(order => order.CreatedAtUtc).IsRequired();
            entity.Property(order => order.ReservedUntilUtc).IsRequired();
            entity.Property(order => order.UpdatedAtUtc).IsRequired();
            entity.HasIndex(order => order.IdempotencyKeyHash).IsUnique();
            entity.HasIndex(order => new { order.State, order.ReservedUntilUtc });
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("OrderItems");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ProductSku).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Quantity).IsRequired();
            entity.Property(item => item.UnitPrice).HasPrecision(12, 2).IsRequired();
            entity.Ignore(item => item.Subtotal);
            entity.HasIndex(item => item.OrderId);
            entity.HasOne(item => item.Order)
                .WithMany(order => order.Items)
                .HasForeignKey(item => item.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Product)
                .WithMany()
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments");
            entity.HasKey(payment => payment.Id);
            entity.Property(payment => payment.ProviderKey).HasMaxLength(64).IsRequired();
            entity.Property(payment => payment.ExternalReference).HasMaxLength(128).IsRequired();
            entity.Property(payment => payment.Status).HasMaxLength(16).IsRequired();
            entity.Property(payment => payment.Amount).HasPrecision(12, 2).IsRequired();
            entity.Property(payment => payment.Currency).HasMaxLength(3).IsRequired();
            entity.Property(payment => payment.CreatedAtUtc).IsRequired();
            entity.Property(payment => payment.UpdatedAtUtc).IsRequired();
            entity.HasIndex(payment => payment.OrderId).IsUnique();
            entity.HasIndex(payment => payment.ExternalReference);
            entity.HasOne(payment => payment.Order)
                .WithMany(order => order.Payments)
                .HasForeignKey(payment => payment.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PaymentProviderSetting>(entity =>
        {
            entity.ToTable("PaymentProviderSettings");
            entity.HasKey(setting => setting.ProviderKey);
            entity.Property(setting => setting.ProviderKey).HasMaxLength(64);
            entity.Property(setting => setting.PublicConfigurationJson).IsRequired();
            entity.Property(setting => setting.ProtectedSecrets).IsRequired();
            entity.Property(setting => setting.IsActive).IsRequired();
            entity.Property(setting => setting.IsEnabled).HasDefaultValue(true).IsRequired();
            entity.Property(setting => setting.UpdatedAtUtc).IsRequired();
            entity.HasIndex(setting => setting.IsActive)
                .IsUnique()
                .HasFilter("\"IsActive\" = TRUE");
        });

        modelBuilder.Entity<ShippingStrategySetting>(entity =>
        {
            entity.ToTable("ShippingStrategySettings");
            entity.HasKey(setting => setting.StrategyKey);
            entity.Property(setting => setting.StrategyKey).HasMaxLength(64);
            entity.Property(setting => setting.PublicConfigurationJson).IsRequired();
            entity.Property(setting => setting.ProtectedSecrets).IsRequired();
            entity.Property(setting => setting.IsEnabled).IsRequired();
            entity.Property(setting => setting.IsActive).IsRequired();
            entity.Property(setting => setting.UpdatedAtUtc).IsRequired();
            entity.HasIndex(setting => setting.IsActive)
                .IsUnique()
                .HasFilter("\"IsActive\" = TRUE");
        });

        modelBuilder.Entity<DataProtectionKey>(entity =>
        {
            entity.ToTable("DataProtectionKeys");
            entity.HasKey(key => key.Id);
            entity.Property(key => key.FriendlyName).HasMaxLength(449).IsRequired();
            entity.Property(key => key.Xml).IsRequired();
            entity.HasIndex(key => key.FriendlyName).IsUnique();
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
