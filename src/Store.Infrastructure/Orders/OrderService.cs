using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Store.Application;
using Store.Domain;

namespace Store.Infrastructure.Orders;

public sealed class OrderService(
    IDbContextFactory<StoreDbContext> contextFactory,
    IShippingStrategyRegistry shippingStrategies,
    ReservationOptions options,
    TimeProvider timeProvider) : IOrderService
{
    private const decimal MaximumAmount = 9_999_999_999.99m;

    public async Task<CartValidationResult> ValidateCartAsync(
        IReadOnlyList<CartItemInput> items,
        CancellationToken cancellationToken)
    {
        var quantities = Consolidate(items);
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var products = await database.Products
            .AsNoTracking()
            .Where(product => quantities.Keys.Contains(product.Id))
            .Select(product => new ProductLine(
                product.Id,
                product.Name,
                product.Sku,
                product.Price,
                product.Stock - product.Reserved,
                product.WeightKg))
            .ToDictionaryAsync(product => product.Id, cancellationToken);
        var subtotal = products.Values.Sum(product =>
            quantities.GetValueOrDefault(product.Id) * product.Price);
        var shipping = await shippingStrategies.CalculateAsync(
            new ShippingContext(
                products.Values.Select(product =>
                    new ShippingItem(quantities.GetValueOrDefault(product.Id), product.WeightKg)).ToArray(),
                subtotal,
                string.Empty),
            cancellationToken);
        return BuildCartValidation(quantities, products, shipping.Amount);
    }

    public async Task<OrderServiceResult> CreateAsync(
        CheckoutInput input,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var quantities = Consolidate(input.Items);
        var idempotencyHash = Hash(idempotencyKey.Trim());
        var address = NormalizeAddress(input.ShippingAddress);
        var requestHash = Hash(JsonSerializer.Serialize(new
        {
            ShippingAddress = address,
            Items = quantities.OrderBy(pair => pair.Key)
        }));
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        var dbTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        await AcquireIdempotencyLockAsync(
            database,
            dbTransaction,
            Hash($"{input.CustomerId:N}:{idempotencyKey.Trim()}"),
            cancellationToken);

        var contactEmail = await database.Users
            .AsNoTracking()
            .Where(user => user.Id == input.CustomerId && user.IsActive)
            .Select(user => user.Email)
            .SingleOrDefaultAsync(cancellationToken);
        if (contactEmail is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new OrderServiceResult(OrderOutcome.NotFound, null, []);
        }

        var existing = await database.Orders
            .Include(order => order.Items)
            .SingleOrDefaultAsync(
                order => order.CustomerId == input.CustomerId && order.IdempotencyKeyHash == idempotencyHash,
                cancellationToken);
        if (existing is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return existing.RequestHash == requestHash
                ? new OrderServiceResult(OrderOutcome.Existing, ToView(existing), [])
                : new OrderServiceResult(OrderOutcome.IdempotencyConflict, null, []);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var reservedItems = new List<OrderItem>();
        var reservedWeights = new Dictionary<Guid, decimal?>();
        foreach (var (productId, quantity) in quantities.OrderBy(pair => pair.Key))
        {
            var reserved = await TryReserveAsync(
                database,
                dbTransaction,
                productId,
                quantity,
                now,
                cancellationToken);
            if (reserved is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                var issues = await GetStockIssuesAsync(quantities, cancellationToken);
                return new OrderServiceResult(OrderOutcome.OutOfStock, null, issues);
            }

            reservedItems.Add(new OrderItem(
                productId,
                reserved.Name,
                reserved.Sku,
                quantity,
                reserved.Price));
            reservedWeights[productId] = reserved.WeightKg;
        }

        var subtotal = reservedItems.Sum(item => item.Subtotal);
        if (subtotal > MaximumAmount)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new OrderServiceResult(OrderOutcome.AmountOutOfRange, null, []);
        }

        var shipping = await shippingStrategies.CalculateAsync(
            new ShippingContext(
                reservedItems.Select(item =>
                    new ShippingItem(item.Quantity, reservedWeights.GetValueOrDefault(item.ProductId))).ToArray(),
                subtotal,
                address.Country),
            cancellationToken);
        var shippingAmount = shipping.Amount;
        if (shippingAmount < 0 || shippingAmount > MaximumAmount - subtotal)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new OrderServiceResult(OrderOutcome.AmountOutOfRange, null, []);
        }

        var orderNumber = OrderNumberFormatter.Format(
            now,
            await NextOrderSequenceAsync(database, dbTransaction, cancellationToken));
        var order = new Order(
            input.CustomerId,
            orderNumber,
            idempotencyHash,
            requestHash,
            contactEmail,
            address,
            reservedItems,
            shippingAmount,
            now,
            now.Add(options.TimeToLive));
        database.Orders.Add(order);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new OrderServiceResult(OrderOutcome.Created, ToView(order), []);
    }

    public async Task<OrderView?> GetAsync(Guid orderId, Guid customerId, CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var order = await database.Orders
            .AsNoTracking()
            .Include(item => item.Items)
            .SingleOrDefaultAsync(
                item => item.Id == orderId && item.CustomerId == customerId,
                cancellationToken);
        return order is null ? null : ToView(order);
    }

    public Task<OrderServiceResult> CancelAsync(
        Guid orderId,
        Guid customerId,
        CancellationToken cancellationToken) =>
        TransitionAndReleaseAsync(orderId, customerId, OrderState.Cancelled, cancellationToken);

    public Task<OrderServiceResult> FailAsync(
        Guid orderId,
        Guid customerId,
        CancellationToken cancellationToken) =>
        TransitionAndReleaseAsync(orderId, customerId, OrderState.Failed, cancellationToken);

    private async Task<OrderServiceResult> TransitionAndReleaseAsync(
        Guid orderId,
        Guid customerId,
        OrderState targetState,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var dbTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        await LockOrderAsync(database, dbTransaction, orderId, cancellationToken);
        var order = await database.Orders
            .Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.Id == orderId, cancellationToken);
        if (order is null || order.CustomerId != customerId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new OrderServiceResult(OrderOutcome.NotFound, null, []);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var transitioned = targetState == OrderState.Cancelled
            ? order.Cancel(now)
            : order.Fail(now);
        if (!transitioned)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new OrderServiceResult(OrderOutcome.AlreadyFinalized, ToView(order), []);
        }

        foreach (var item in order.Items)
        {
            await ReleaseAsync(database, dbTransaction, item.ProductId, item.Quantity, now, cancellationToken);
            database.StockMovements.Add(new StockMovement(
                item.ProductId,
                order.Id,
                "ReservationReleased",
                item.Quantity,
                now));
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new OrderServiceResult(OrderOutcome.Created, ToView(order), []);
    }

    private async Task<IReadOnlyList<StockIssue>> GetStockIssuesAsync(
        IReadOnlyDictionary<Guid, int> quantities,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var current = await database.Products
            .AsNoTracking()
            .Where(product => quantities.Keys.Contains(product.Id))
            .Select(product => new { product.Id, Available = product.Stock - product.Reserved })
            .ToDictionaryAsync(product => product.Id, cancellationToken);
        return quantities
            .Where(pair => !current.TryGetValue(pair.Key, out var item) || item.Available < pair.Value)
            .Select(pair => new StockIssue(
                pair.Key,
                pair.Value,
                current.TryGetValue(pair.Key, out var item) ? item.Available : 0))
            .ToArray();
    }

    private CartValidationResult BuildCartValidation(
        IReadOnlyDictionary<Guid, int> quantities,
        IReadOnlyDictionary<Guid, ProductLine> products,
        decimal shipping)
    {
        var lines = new List<CartLineView>();
        var issues = new List<StockIssue>();
        foreach (var (productId, quantity) in quantities.OrderBy(pair => pair.Key))
        {
            if (!products.TryGetValue(productId, out var product))
            {
                issues.Add(new StockIssue(productId, quantity, 0));
                continue;
            }

            if (product.Available < quantity)
            {
                issues.Add(new StockIssue(productId, quantity, product.Available));
            }

            lines.Add(new CartLineView(
                productId,
                product.Name,
                product.Sku,
                quantity,
                product.Available,
                product.Price,
                product.Price * quantity));
        }

        var subtotal = lines.Sum(line => line.Subtotal);
        return new CartValidationResult(
            issues.Count == 0,
            lines,
            issues,
            subtotal,
            shipping,
            subtotal + shipping);
    }

    private static IReadOnlyDictionary<Guid, int> Consolidate(IReadOnlyList<CartItemInput> items)
    {
        var quantities = new Dictionary<Guid, int>();
        foreach (var item in items)
        {
            if (item.ProductId == Guid.Empty || item.Quantity < 1)
            {
                throw new ArgumentException("Cart items must have a product ID and positive quantity.");
            }

            quantities[item.ProductId] = checked(quantities.GetValueOrDefault(item.ProductId) + item.Quantity);
        }

        return quantities;
    }

    private async Task<ReservedProduct?> TryReserveAsync(
        StoreDbContext database,
        NpgsqlTransaction transaction,
        Guid productId,
        int quantity,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            UPDATE "Products"
            SET "Reserved" = "Reserved" + @quantity,
                "UpdatedAtUtc" = @updatedAtUtc
            WHERE "Id" = @productId
              AND NOT "IsDeleted"
              AND "Stock" - "Reserved" >= @quantity
            RETURNING "Name", "Sku", "Price", "WeightKg"
            """,
            (NpgsqlConnection)database.Database.GetDbConnection(),
            transaction);
        command.Parameters.AddWithValue("quantity", quantity);
        command.Parameters.AddWithValue("productId", productId);
        command.Parameters.AddWithValue("updatedAtUtc", updatedAtUtc);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ReservedProduct(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetDecimal(2),
                reader.IsDBNull(3) ? null : reader.GetDecimal(3))
            : null;
    }

    internal static async Task ReleaseAsync(
        StoreDbContext database,
        NpgsqlTransaction transaction,
        Guid productId,
        int quantity,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            UPDATE "Products"
            SET "Reserved" = "Reserved" - @quantity,
                "UpdatedAtUtc" = @updatedAtUtc
            WHERE "Id" = @productId AND "Reserved" >= @quantity
            """,
            (NpgsqlConnection)database.Database.GetDbConnection(),
            transaction);
        command.Parameters.AddWithValue("quantity", quantity);
        command.Parameters.AddWithValue("productId", productId);
        command.Parameters.AddWithValue("updatedAtUtc", updatedAtUtc);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException("The reserved inventory could not be released.");
        }
    }

    private static async Task AcquireIdempotencyLockAsync(
        StoreDbContext database,
        NpgsqlTransaction transaction,
        string idempotencyHash,
        CancellationToken cancellationToken)
    {
        var lockKey = BinaryPrimitives.ReadInt64BigEndian(Convert.FromHexString(idempotencyHash));
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(@lockKey)",
            (NpgsqlConnection)database.Database.GetDbConnection(),
            transaction);
        command.Parameters.AddWithValue("lockKey", lockKey);
        await command.ExecuteScalarAsync(cancellationToken);
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

    private static async Task<long> NextOrderSequenceAsync(
        StoreDbContext database,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT nextval('\"OrderNumberSequence\"')",
            (NpgsqlConnection)database.Database.GetDbConnection(),
            transaction);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static ShippingAddress NormalizeAddress(ShippingAddressInput input) => new(
        input.RecipientName.Trim(),
        input.Line1.Trim(),
        string.IsNullOrWhiteSpace(input.Line2) ? null : input.Line2.Trim(),
        input.City.Trim(),
        input.Region.Trim(),
        input.PostalCode.Trim(),
        input.Country.Trim());

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static OrderView ToView(Order order) => new(
        order.Id,
        order.OrderNumber,
        order.State.ToString(),
        order.ReservedUntilUtc,
        order.CreatedAtUtc,
        order.Subtotal,
        order.Shipping,
        order.Total,
        order.ContactEmail,
        new ShippingAddressView(
            order.ShippingAddress.RecipientName,
            order.ShippingAddress.Line1,
            order.ShippingAddress.Line2,
            order.ShippingAddress.City,
            order.ShippingAddress.Region,
            order.ShippingAddress.PostalCode,
            order.ShippingAddress.Country),
        order.Items
            .OrderBy(item => item.ProductName)
            .Select(item => new OrderItemView(
                item.ProductId,
                item.ProductName,
                item.ProductSku,
                item.Quantity,
                item.UnitPrice,
                item.Subtotal))
            .ToArray());

    private sealed record ProductLine(
        Guid Id,
        string Name,
        string Sku,
        decimal Price,
        int Available,
        decimal? WeightKg);

    private sealed record ReservedProduct(string Name, string Sku, decimal Price, decimal? WeightKg);
}
