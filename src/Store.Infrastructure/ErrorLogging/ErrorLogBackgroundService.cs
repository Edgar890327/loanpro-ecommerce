using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Store.Domain;

namespace Store.Infrastructure.ErrorLogging;

public sealed class ErrorLogBackgroundService(
    ErrorLogQueue queue,
    IErrorLogBatchStore batchStore,
    IOptions<ErrorLogOptions> options,
    ILogger<ErrorLogBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        while (await queue.Reader.WaitToReadAsync(stoppingToken))
        {
            var batch = await ReadBatchAsync(settings, stoppingToken);

            try
            {
                await batchStore.InsertBatchAsync(batch, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to persist an error log batch of {ErrorLogCount}; the batch was written to the Serilog console fallback.",
                    batch.Count);
            }
        }
    }

    private async Task<List<ErrorLog>> ReadBatchAsync(
        ErrorLogOptions settings,
        CancellationToken stoppingToken)
    {
        var batch = new List<ErrorLog>(settings.BatchSize);
        if (queue.Reader.TryRead(out var first))
        {
            batch.Add(first);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(settings.BatchInterval);

        while (batch.Count < settings.BatchSize)
        {
            while (batch.Count < settings.BatchSize && queue.Reader.TryRead(out var errorLog))
            {
                batch.Add(errorLog);
            }

            if (batch.Count == settings.BatchSize)
            {
                break;
            }

            try
            {
                if (!await queue.Reader.WaitToReadAsync(timeout.Token))
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        return batch;
    }
}
