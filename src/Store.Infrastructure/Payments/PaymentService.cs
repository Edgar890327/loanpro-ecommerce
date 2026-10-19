using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Store.Application;
using Store.Domain;
using Store.Infrastructure.Orders;

namespace Store.Infrastructure.Payments;

public sealed class PaymentService(
    IDbContextFactory<StoreDbContext> contextFactory,
    IPaymentProviderRegistry registry,
    IPaymentProviderConfigurationReader configurationReader,
    TimeProvider timeProvider) : IPaymentService
{
    public async Task<PaymentServiceResult> CreateAsync(
        Guid orderId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var dbTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        await LockOrderAsync(database, dbTransaction, orderId, cancellationToken);
        var order = await database.Orders
            .Include(item => item.Items)
            .Include(item => item.Payments)
            .SingleOrDefaultAsync(item => item.Id == orderId && item.CustomerId == customerId, cancellationToken);
        if (order is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result(PaymentOutcome.NotFound);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (order.State == OrderState.Expired)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result(PaymentOutcome.Expired);
        }

        if (order.State != OrderState.Reserved)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result(PaymentOutcome.AlreadyFinalized);
        }

        if (order.ReservedUntilUtc <= now)
        {
            await ExpireOrderAsync(database, dbTransaction, order, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result(PaymentOutcome.Expired);
        }

        var payment = order.Payments.SingleOrDefault();
        if (payment is not null)
        {
            var configured = await registry.GetAsync(payment.ProviderKey, cancellationToken);
            if (configured is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result(PaymentOutcome.ProviderUnavailable);
            }

            await transaction.CommitAsync(cancellationToken);
            return CreationResult(payment, configured, order, existing: true);
        }

        var active = await registry.GetActiveAsync(cancellationToken);
        if (active is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result(PaymentOutcome.ProviderUnavailable);
        }

        try
        {
            var provider = registry.Resolve(active.Descriptor.Key);
            var configuration = await configurationReader.GetConfigurationAsync(
                active.Descriptor.Key,
                cancellationToken);
            var created = await provider.CreatePaymentAsync(
                new PaymentProviderContext(order.Id.ToString("N"), configuration),
                ToPaymentOrder(order),
                cancellationToken);
            payment = new Payment(
                order.Id,
                provider.Descriptor.Key,
                created.ExternalReference,
                "Created",
                order.Total,
                now);
            database.Payments.Add(payment);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return CreationResult(payment, active, order, existing: false);
        }
        catch (PaymentProviderException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result(PaymentOutcome.ProviderUnavailable);
        }
    }

    public async Task<PaymentServiceResult> CaptureAsync(
        Guid orderId,
        Guid customerId,
        string externalReference,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var dbTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        await LockOrderAsync(database, dbTransaction, orderId, cancellationToken);
        var order = await database.Orders
            .Include(item => item.Items)
            .Include(item => item.Payments)
            .SingleOrDefaultAsync(item => item.Id == orderId && item.CustomerId == customerId, cancellationToken);
        if (order is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result(PaymentOutcome.NotFound);
        }

        var payment = order.Payments.SingleOrDefault();
        if (payment is null || payment.ExternalReference != externalReference)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result(PaymentOutcome.NotFound);
        }

        if (order.State == OrderState.Paid)
        {
            await transaction.CommitAsync(cancellationToken);
            return CaptureResult(order, PaymentResultStatus.Approved, PaymentOutcome.Approved);
        }

        if (order.State == OrderState.Expired)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result(PaymentOutcome.Expired);
        }

        if (order.State != OrderState.Reserved)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result(PaymentOutcome.AlreadyFinalized);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (order.ReservedUntilUtc <= now)
        {
            await ExpireOrderAsync(database, dbTransaction, order, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result(PaymentOutcome.Expired);
        }

        try
        {
            var provider = registry.Resolve(payment.ProviderKey);
            var configuration = await configurationReader.GetConfigurationAsync(
                payment.ProviderKey,
                cancellationToken);
            var captured = await provider.CapturePaymentAsync(
                new PaymentProviderContext(order.Id.ToString("N"), configuration),
                ToPaymentOrder(order),
                payment.ExternalReference,
                cancellationToken);
            if (captured.Amount != order.Total ||
                !string.Equals(captured.Currency, "USD", StringComparison.OrdinalIgnoreCase))
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result(PaymentOutcome.ProviderResponseInvalid);
            }

            switch (captured.Status)
            {
                case PaymentResultStatus.Approved:
                    if (!order.MarkPaid(now))
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result(PaymentOutcome.Expired);
                    }

                    foreach (var item in order.Items)
                    {
                        await ConvertReservedToSoldAsync(
                            database,
                            dbTransaction,
                            item.ProductId,
                            item.Quantity,
                            now,
                            cancellationToken);
                        database.StockMovements.Add(new StockMovement(
                            item.ProductId,
                            order.Id,
                            "Sale",
                            item.Quantity,
                            now));
                    }

                    payment.Update(payment.ExternalReference, "Approved", now);
                    break;
                case PaymentResultStatus.Failed:
                    order.Fail(now);
                    await ReleaseReservationAsync(
                        database,
                        dbTransaction,
                        order,
                        "ReservationReleased",
                        now,
                        cancellationToken);
                    payment.Update(payment.ExternalReference, "Failed", now);
                    break;
                case PaymentResultStatus.Pending:
                    payment.Update(payment.ExternalReference, "Pending", now);
                    await database.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return CaptureResult(order, PaymentResultStatus.Pending, PaymentOutcome.Pending);
                default:
                    await transaction.RollbackAsync(cancellationToken);
                    return Result(PaymentOutcome.ProviderResponseInvalid);
            }

            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return CaptureResult(order, captured.Status, Outcome(captured.Status));
        }
        catch (PaymentProviderException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result(PaymentOutcome.ProviderUnavailable);
        }
    }

    private static PaymentServiceResult CreationResult(
        Payment payment,
        PaymentProviderView provider,
        Order order,
        bool existing) =>
        new(
            existing ? PaymentOutcome.Existing : PaymentOutcome.Created,
            new PaymentCreationView(
                order.Id,
                payment.ProviderKey,
                payment.ExternalReference,
                provider.Descriptor,
                provider.PublicConfiguration),
            null);

    private static PaymentServiceResult CaptureResult(
        Order order,
        PaymentResultStatus status,
        PaymentOutcome outcome) =>
        new(outcome, null, new PaymentCaptureView(order.Id, order.State.ToString(), status));

    private static PaymentServiceResult Result(PaymentOutcome outcome) => new(outcome, null, null);

    private static PaymentOutcome Outcome(PaymentResultStatus status) => status switch
    {
        PaymentResultStatus.Approved => PaymentOutcome.Approved,
        PaymentResultStatus.Failed => PaymentOutcome.Failed,
        _ => PaymentOutcome.Pending
    };

    private async Task ExpireOrderAsync(
        StoreDbContext database,
        NpgsqlTransaction transaction,
        Order order,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!order.Expire(now))
        {
            return;
        }

        await ReleaseReservationAsync(
            database,
            transaction,
            order,
            "ReservationExpired",
            now,
            cancellationToken);
    }

    private static async Task ReleaseReservationAsync(
        StoreDbContext database,
        NpgsqlTransaction transaction,
        Order order,
        string movementType,
        DateTime now,
        CancellationToken cancellationToken)
    {
        foreach (var item in order.Items)
        {
            await OrderService.ReleaseAsync(
                database,
                transaction,
                item.ProductId,
                item.Quantity,
                now,
                cancellationToken);
            database.StockMovements.Add(new StockMovement(
                item.ProductId,
                order.Id,
                movementType,
                item.Quantity,
                now));
        }
    }

    private static async Task ConvertReservedToSoldAsync(
        StoreDbContext database,
        NpgsqlTransaction transaction,
        Guid productId,
        int quantity,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            UPDATE "Products"
            SET "Stock" = "Stock" - @quantity,
                "Reserved" = "Reserved" - @quantity,
                "UpdatedAtUtc" = @now
            WHERE "Id" = @productId
              AND "Stock" >= @quantity
              AND "Reserved" >= @quantity
            """,
            (NpgsqlConnection)database.Database.GetDbConnection(),
            transaction);
        command.Parameters.AddWithValue("quantity", quantity);
        command.Parameters.AddWithValue("productId", productId);
        command.Parameters.AddWithValue("now", now);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException("Reserved inventory could not be converted to a sale.");
        }
    }

    private static async Task LockOrderAsync(
        StoreDbContext database,
        NpgsqlTransaction transaction,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT \"Id\" FROM \"Orders\" WHERE \"Id\" = @orderId FOR UPDATE",
            (NpgsqlConnection)database.Database.GetDbConnection(),
            transaction);
        command.Parameters.AddWithValue("orderId", orderId);
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static PaymentOrder ToPaymentOrder(Order order) => new(
        order.Id,
        order.Subtotal,
        order.Shipping,
        order.Total,
        order.ReservedUntilUtc,
        order.Items.Select(item => new PaymentOrderLine(
            item.ProductId,
            item.ProductName,
            item.ProductSku,
            item.Quantity,
            item.UnitPrice,
            item.Subtotal)).ToArray());
}
