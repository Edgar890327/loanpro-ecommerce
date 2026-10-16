using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Store.Domain;

namespace Store.Infrastructure.Orders;

public sealed class ReservationExpirationService(
    IDbContextFactory<StoreDbContext> contextFactory,
    ReservationOptions options,
    TimeProvider timeProvider,
    ILogger<ReservationExpirationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.ScanInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ExpireBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to expire reserved orders.");
            }
        }
    }

    private async Task ExpireBatchAsync(CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        var dbTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var orderIds = new List<Guid>();
        await using (var command = new NpgsqlCommand(
                         """
                         SELECT "Id"
                         FROM "Orders"
                         WHERE "State" = 'Reserved' AND "ReservedUntilUtc" <= @now
                         ORDER BY "ReservedUntilUtc", "Id"
                         LIMIT @batchSize
                         FOR UPDATE SKIP LOCKED
                         """,
                         (NpgsqlConnection)database.Database.GetDbConnection(),
                         dbTransaction))
        {
            command.Parameters.AddWithValue("now", now);
            command.Parameters.AddWithValue("batchSize", options.BatchSize);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                orderIds.Add(reader.GetGuid(0));
            }
        }

        if (orderIds.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var orders = await database.Orders
            .Include(order => order.Items)
            .Where(order => orderIds.Contains(order.Id))
            .ToListAsync(cancellationToken);
        foreach (var order in orders)
        {
            if (!order.Expire(now))
            {
                continue;
            }

            foreach (var item in order.Items)
            {
                await OrderService.ReleaseAsync(
                    database,
                    dbTransaction,
                    item.ProductId,
                    item.Quantity,
                    now,
                    cancellationToken);
                database.StockMovements.Add(new StockMovement(
                    item.ProductId,
                    order.Id,
                    "ReservationExpired",
                    item.Quantity,
                    now));
            }
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
