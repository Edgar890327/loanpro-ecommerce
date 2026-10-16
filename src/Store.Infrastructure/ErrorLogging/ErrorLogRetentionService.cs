using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Store.Infrastructure.ErrorLogging;

public sealed class ErrorLogRetentionService(
    IErrorLogBatchStore batchStore,
    IOptions<ErrorLogOptions> options,
    ILogger<ErrorLogRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(settings.RetentionInterval, stoppingToken);
                var cutoffUtc = DateTime.UtcNow.AddDays(-settings.RetentionDays);
                await batchStore.DeleteOlderThanAsync(cutoffUtc, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to delete expired error logs.");
            }
        }
    }
}
