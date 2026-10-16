using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Store.Application;
using Store.Infrastructure.ErrorLogging;
using Store.Infrastructure.Importing;

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
        services.AddSingleton<ProductCsvParser>();
        services.AddScoped<ProductImportService>();
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
