using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Store.Application;
using Store.Infrastructure.Catalog;
using Store.Infrastructure.ErrorLogging;
using Store.Infrastructure.E2e;
using Store.Infrastructure.Importing;
using Store.Infrastructure.Orders;
using Store.Infrastructure.Payments;

namespace Store.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The connection string 'DefaultConnection' is required.");
        }

        services.AddDbContextFactory<StoreDbContext>(options => options.UseNpgsql(connectionString));
        services.AddMemoryCache();
        services.AddDataProtection().SetApplicationName("LoanPro.Store");
        services.AddSingleton<IXmlRepository, EfCoreXmlRepository>();
        services.AddSingleton<IConfigureOptions<KeyManagementOptions>, DatabaseKeyManagementOptions>();
        services.AddSingleton<PaymentSecretsProtector>();
        services.AddSingleton<IPaymentProvider, SimulatedPaymentProvider>();
        services.AddHttpClient<PayPalPaymentProvider>(client =>
            client.Timeout = TimeSpan.FromSeconds(15));
        services.AddTransient<IPaymentProvider>(provider => provider.GetRequiredService<PayPalPaymentProvider>());
        services.AddScoped<PaymentProviderRegistry>();
        services.AddScoped<IPaymentProviderRegistry>(provider => provider.GetRequiredService<PaymentProviderRegistry>());
        services.AddScoped<IPaymentProviderConfigurationReader>(
            provider => provider.GetRequiredService<PaymentProviderRegistry>());
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IDatabaseHealthReader, DatabaseHealthReader>();
        services.AddScoped<IProductCatalogReader, ProductCatalogReader>();
        services.AddScoped<IProductAdministration, Store.Infrastructure.Products.ProductAdministration>();
        services.AddScoped<IErrorLogReader, ErrorLogReader>();
        services.AddSingleton<ProductCsvParser>();
        services.AddScoped<IProductImportService, ProductImportService>();
        services.AddSingleton<IShippingStrategy, FlatRateShippingStrategy>();
        services.AddScoped<ShippingStrategyRegistry>();
        services.AddScoped<IShippingStrategyRegistry>(
            provider => provider.GetRequiredService<ShippingStrategyRegistry>());
        services.AddScoped<IShippingQuoteService, ShippingQuoteService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IMyOrdersReader, Store.Infrastructure.MyOrders.MyOrdersReader>();
        services.AddSingleton<IPasswordHasher, Store.Infrastructure.Identity.AspNetPasswordHasher>();
        services.AddScoped<IUserRepository, Store.Infrastructure.Identity.UserRepository>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<AdminSeeder>();
        services.AddScoped<IOrderHistoryReader, OrderHistoryReader>();
        services.AddScoped<IE2eDataReset, E2eDataReset>();
        var reservationTtlSeconds = configuration.GetValue<int?>("RESERVATION_TTL_SECONDS");
        var reservationTtlMinutes = configuration.GetValue<int>("RESERVATION_TTL_MINUTES", 15);
        var reservationOptions = new ReservationOptions
        {
            TimeToLive = reservationTtlSeconds.HasValue
                ? TimeSpan.FromSeconds(reservationTtlSeconds.Value)
                : TimeSpan.FromMinutes(reservationTtlMinutes),
            ScanInterval = TimeSpan.FromSeconds(
                configuration.GetValue<int>("RESERVATION_SCAN_INTERVAL_SECONDS", 15)),
            BatchSize = configuration.GetValue<int>("RESERVATION_EXPIRATION_BATCH_SIZE", 100)
        };
        if (reservationOptions.TimeToLive <= TimeSpan.Zero ||
            reservationOptions.ScanInterval <= TimeSpan.Zero ||
            reservationOptions.BatchSize < 1)
        {
            throw new InvalidOperationException("Reservation settings must be positive.");
        }

        services.AddSingleton(reservationOptions);
        services.AddHostedService<ReservationExpirationService>();
        var errorLogOptions = configuration.GetSection(ErrorLogOptions.SectionName)
            .Get<ErrorLogOptions>() ?? new ErrorLogOptions();
        var retentionDays = configuration.GetValue<int?>("ERROR_LOG_RETENTION_DAYS");
        if (retentionDays.HasValue)
        {
            errorLogOptions.RetentionDays = retentionDays.Value;
        }

        services.AddSingleton(errorLogOptions);
        services.AddSingleton<ErrorLogQueue>();
        services.AddSingleton<IErrorLogWriter>(provider => provider.GetRequiredService<ErrorLogQueue>());
        services.AddSingleton<IErrorLogBatchStore, EfCoreErrorLogBatchStore>();
        services.AddHostedService<ErrorLogBackgroundService>();
        services.AddHostedService<ErrorLogRetentionService>();
        services.AddOptions<ErrorLogOptions>()
            .Configure(options =>
            {
                options.BatchSize = errorLogOptions.BatchSize;
                options.QueueCapacity = errorLogOptions.QueueCapacity;
                options.RetentionDays = errorLogOptions.RetentionDays;
                options.BatchInterval = errorLogOptions.BatchInterval;
                options.RetentionInterval = errorLogOptions.RetentionInterval;
            })
            .Validate(options => options.BatchSize > 0, "Error log batch size must be positive.")
            .Validate(options => options.QueueCapacity >= options.BatchSize,
                "Error log queue capacity must be at least the batch size.")
            .Validate(options => options.RetentionDays > 0,
                "ERROR_LOG_RETENTION_DAYS must be positive.")
            .Validate(options => options.BatchInterval > TimeSpan.Zero,
                "Error log batch interval must be positive.")
            .Validate(options => options.RetentionInterval > TimeSpan.Zero,
                "Error log retention interval must be positive.")
            .ValidateOnStart();
        return services;
    }
}
