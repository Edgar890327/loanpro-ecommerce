using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Store.Domain;
using Store.Infrastructure.ErrorLogging;

namespace Store.UnitTests;

public sealed class ErrorLogBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_PersistsQueuedErrorsInBatches()
    {
        var options = CreateOptions(batchSize: 2);
        var queue = new ErrorLogQueue(options.Value);
        var store = new FakeErrorLogBatchStore();
        using var service = new ErrorLogBackgroundService(
            queue,
            store,
            options,
            NullLogger<ErrorLogBackgroundService>.Instance);
        await service.StartAsync(CancellationToken.None);

        Assert.True(queue.TryWrite(CreateErrorLog()));
        Assert.True(queue.TryWrite(CreateErrorLog()));

        var batch = await store.WaitForBatchAsync(0);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(2, batch.Count);
        Assert.All(batch, errorLog => Assert.Equal("Error", errorLog.Level));
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotPropagateInsertFailureAndContinuesProcessing()
    {
        var options = CreateOptions(batchSize: 1);
        var queue = new ErrorLogQueue(options.Value);
        var store = new FakeErrorLogBatchStore
        {
            Insert = call => call == 0
                ? Task.FromException(new InvalidOperationException("database unavailable"))
                : Task.CompletedTask
        };
        using var service = new ErrorLogBackgroundService(
            queue,
            store,
            options,
            NullLogger<ErrorLogBackgroundService>.Instance);
        await service.StartAsync(CancellationToken.None);

        Assert.True(queue.TryWrite(CreateErrorLog()));
        await store.WaitForBatchAsync(0);
        Assert.True(queue.TryWrite(CreateErrorLog()));
        await store.WaitForBatchAsync(1);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(2, store.Batches.Count);
    }

    [Fact]
    public async Task RetentionService_DeletesUsingConfiguredRetentionDays()
    {
        var options = Options.Create(new ErrorLogOptions
        {
            RetentionDays = 30,
            RetentionInterval = TimeSpan.FromMilliseconds(1)
        });
        var store = new FakeErrorLogBatchStore();
        using var service = new ErrorLogRetentionService(
            store,
            options,
            NullLogger<ErrorLogRetentionService>.Instance);
        var startedAt = DateTime.UtcNow;
        await service.StartAsync(CancellationToken.None);

        var cutoff = await store.WaitForDeleteAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.InRange(cutoff, startedAt.AddDays(-30), DateTime.UtcNow.AddDays(-30));
    }

    private static IOptions<ErrorLogOptions> CreateOptions(int batchSize) =>
        Options.Create(new ErrorLogOptions
        {
            BatchSize = batchSize,
            QueueCapacity = 10,
            BatchInterval = TimeSpan.FromMilliseconds(50)
        });

    private static ErrorLog CreateErrorLog() => new()
    {
        TimestampUtc = DateTime.UtcNow,
        Level = "Error",
        CorrelationId = Guid.NewGuid().ToString("D"),
        Message = "An unhandled exception occurred.",
        ExceptionType = typeof(InvalidOperationException).FullName!,
        HttpMethod = "GET",
        Path = "/",
        StatusCode = 500
    };

    private sealed class FakeErrorLogBatchStore : IErrorLogBatchStore
    {
        private readonly ConcurrentQueue<IReadOnlyCollection<ErrorLog>> _batches = new();
        private readonly ConcurrentQueue<TaskCompletionSource<IReadOnlyCollection<ErrorLog>>> _batchWaiters = new();
        private readonly TaskCompletionSource<DateTime> _deleteCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _insertCallCount;

        public Func<int, Task> Insert { get; init; } = _ => Task.CompletedTask;

        public IReadOnlyCollection<IReadOnlyCollection<ErrorLog>> Batches => _batches.ToArray();

        public async Task InsertBatchAsync(
            IReadOnlyCollection<ErrorLog> errorLogs,
            CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _insertCallCount) - 1;
            _batches.Enqueue(errorLogs);
            while (_batchWaiters.TryDequeue(out var waiter))
            {
                waiter.TrySetResult(errorLogs);
            }

            await Insert(call);
        }

        public Task DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
        {
            _deleteCompletion.TrySetResult(cutoffUtc);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<ErrorLog>> WaitForBatchAsync(int index)
        {
            if (_batches.ElementAtOrDefault(index) is { } batch)
            {
                return Task.FromResult(batch);
            }

            var waiter = new TaskCompletionSource<IReadOnlyCollection<ErrorLog>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _batchWaiters.Enqueue(waiter);
            return waiter.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        public Task<DateTime> WaitForDeleteAsync() =>
            _deleteCompletion.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
