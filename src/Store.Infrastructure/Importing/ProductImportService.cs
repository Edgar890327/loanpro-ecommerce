using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using Store.Application;
using Store.Domain;

namespace Store.Infrastructure.Importing;

public sealed class ProductImportService(
    IDbContextFactory<StoreDbContext> contextFactory,
    ProductCsvParser parser,
    TimeProvider timeProvider)
{
    public async Task<ProductImportResult> ImportAsync(
        byte[] fileBytes,
        bool dryRun,
        bool confirmReimport,
        string importedBy,
        CancellationToken cancellationToken)
    {
        var fileHash = Convert.ToHexString(SHA256.HashData(fileBytes));
        using var stream = new MemoryStream(fileBytes, writable: false);
        var parsed = parser.Parse(stream);
        var plan = ProductCsvImportPlanner.Build(parsed.Rows);
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);

        if (dryRun)
        {
            var previous = await FindPreviousBatchAsync(database, fileHash, cancellationToken);
            return await SimulateAsync(
                database,
                plan.Groups,
                parsed.Rows,
                fileHash,
                plan.ExactDuplicates,
                previous,
                dryRun: true,
                cancellationToken);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        var dbTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        await AcquireHashLockAsync(database, dbTransaction, fileHash, cancellationToken);
        var previousBatch = await FindPreviousBatchAsync(database, fileHash, cancellationToken);
        if (previousBatch is not null && !confirmReimport)
        {
            var preview = await SimulateAsync(
                database,
                plan.Groups,
                parsed.Rows,
                fileHash,
                plan.ExactDuplicates,
                previousBatch,
                dryRun: false,
                cancellationToken);
            await transaction.RollbackAsync(cancellationToken);
            return preview with { ReimportConfirmationRequired = true };
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var created = 0;
        long stockAdded = 0;
        var movements = new List<(Guid ProductId, int Quantity)>();
        foreach (var group in plan.Groups)
        {
            var categoryId = await FindOrCreateCategoryAsync(
                database,
                dbTransaction,
                group.First.Category,
                cancellationToken);
            var importedProduct = await UpsertProductAsync(
                database,
                dbTransaction,
                group,
                categoryId,
                now,
                cancellationToken);
            if (importedProduct is null)
            {
                var deleted = await IsDeletedProductAsync(
                    database,
                    group.NormalizedSku,
                    cancellationToken);
                foreach (var row in group.Rows)
                {
                    row.RejectWith(deleted
                        ? "The SKU belongs to a logically deleted product."
                        : "The resulting stock would exceed the supported maximum.");
                }

                continue;
            }

            if (importedProduct.Created)
            {
                created++;
            }

            stockAdded += group.Stock;
            foreach (var row in group.Rows)
            {
                AddDifferencesAgainstExisting(row, importedProduct, group.First.RowNumber);
            }

            movements.Add((importedProduct.Id, group.Stock));
        }

        var summary = CreateSummary(parsed.Rows, created, stockAdded, plan.ExactDuplicates);
        var batch = new ImportBatch(
            fileHash,
            now,
            parsed.Rows.Count,
            summary.CreatedProducts,
            summary.StockAdded,
            summary.Warnings,
            summary.Rejected,
            summary.ExactDuplicates,
            importedBy);
        foreach (var movement in movements)
        {
            database.StockMovements.Add(new StockMovement(
                movement.ProductId,
                batch.Id,
                movement.Quantity,
                now));
        }

        database.ImportBatches.Add(batch);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ProductImportResult(
            CreateReport(fileHash, false, previousBatch is not null, previousBatch?.CreatedAtUtc, summary, parsed.Rows),
            false);
    }

    private static async Task<ImportBatch?> FindPreviousBatchAsync(
        StoreDbContext database,
        string fileHash,
        CancellationToken cancellationToken) =>
        await database.ImportBatches
            .AsNoTracking()
            .Where(batch => batch.FileHash == fileHash)
            .OrderByDescending(batch => batch.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<ProductImportResult> SimulateAsync(
        StoreDbContext database,
        IReadOnlyList<ProductCsvGroup> groups,
        IReadOnlyList<ProductCsvRow> rows,
        string fileHash,
        int exactDuplicates,
        ImportBatch? previous,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var normalizedSkus = groups.Select(group => group.NormalizedSku).ToArray();
        var existingProducts = await database.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(product => normalizedSkus.Contains(product.SkuNormalized))
            .Select(product => new ImportedProduct(
                product.Id,
                product.SkuNormalized,
                false,
                product.IsDeleted,
                product.Name,
                product.Description,
                product.Price,
                product.Stock))
            .ToDictionaryAsync(product => product.Sku, cancellationToken);
        var created = 0;
        long stockAdded = 0;
        foreach (var group in groups)
        {
            if (!existingProducts.TryGetValue(group.NormalizedSku, out var existing))
            {
                created++;
                stockAdded += group.Stock;
                continue;
            }

            if (existing.IsDeleted)
            {
                foreach (var row in group.Rows)
                {
                    row.RejectWith("The SKU belongs to a logically deleted product.");
                }

                continue;
            }

            if (existing.Stock > int.MaxValue - group.Stock)
            {
                foreach (var row in group.Rows)
                {
                    row.RejectWith("The resulting stock would exceed the supported maximum.");
                }

                continue;
            }

            stockAdded += group.Stock;
            foreach (var row in group.Rows)
            {
                AddDifferencesAgainstExisting(row, existing, group.First.RowNumber);
            }
        }

        var summary = CreateSummary(rows, created, stockAdded, exactDuplicates);
        return new ProductImportResult(
            CreateReport(fileHash, dryRun, previous is not null, previous?.CreatedAtUtc, summary, rows),
            previous is not null && !dryRun);
    }

    private static ProductImportSummary CreateSummary(
        IReadOnlyList<ProductCsvRow> rows,
        int created,
        long stockAdded,
        int exactDuplicates) =>
        new(
            created,
            stockAdded,
            rows.Count(row => row.Status == "imported_with_warning"),
            rows.Count(row => row.Status == "rejected"),
            exactDuplicates);

    private static ProductImportReport CreateReport(
        string fileHash,
        bool dryRun,
        bool reimportDetected,
        DateTime? previousImportAtUtc,
        ProductImportSummary summary,
        IReadOnlyList<ProductCsvRow> rows) =>
        new(
            fileHash,
            dryRun,
            reimportDetected,
            previousImportAtUtc,
            summary,
            rows.Select(row => new ProductImportRowReport(row.RowNumber, row.Status, row.Messages.ToArray()))
                .ToArray());

    private static void AddDifferencesAgainstExisting(
        ProductCsvRow row,
        ImportedProduct existing,
        int firstRowNumber)
    {
        if (!existing.Created && existing.Name != row.Name.Trim())
        {
            row.AddWarning($"Name differs from existing product (first file row {firstRowNumber}); existing data was retained.");
        }

        if (!existing.Created && existing.Description != row.Description.Trim())
        {
            row.AddWarning($"Description differs from existing product (first file row {firstRowNumber}); existing data was retained.");
        }

        if (!existing.Created && existing.Price != row.Price)
        {
            row.AddWarning($"Price differs from existing product (first file row {firstRowNumber}); existing data was retained.");
        }
    }

    private static async Task AcquireHashLockAsync(
        StoreDbContext database,
        NpgsqlTransaction transaction,
        string fileHash,
        CancellationToken cancellationToken)
    {
        var lockKey = BitConverter.ToInt64(Convert.FromHexString(fileHash), 0);
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(@lockKey)",
            (NpgsqlConnection)database.Database.GetDbConnection(),
            transaction);
        command.Parameters.AddWithValue("lockKey", lockKey);
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task<Guid> FindOrCreateCategoryAsync(
        StoreDbContext database,
        NpgsqlTransaction transaction,
        string name,
        CancellationToken cancellationToken)
    {
        var normalizedName = name.Trim().ToUpperInvariant();
        var connection = (NpgsqlConnection)database.Database.GetDbConnection();
        await using (var insert = new NpgsqlCommand(
                         """
                         INSERT INTO "Categories" ("Id", "Name", "NormalizedName")
                         VALUES (@id, @name, @normalizedName)
                         ON CONFLICT ("NormalizedName") DO NOTHING
                         """,
                         connection,
                         transaction))
        {
            insert.Parameters.AddWithValue("id", Guid.NewGuid());
            insert.Parameters.AddWithValue("name", name.Trim());
            insert.Parameters.AddWithValue("normalizedName", normalizedName);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var select = new NpgsqlCommand(
            "SELECT \"Id\" FROM \"Categories\" WHERE \"NormalizedName\" = @normalizedName",
            connection,
            transaction);
        select.Parameters.AddWithValue("normalizedName", normalizedName);
        return (Guid)(await select.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("The imported category could not be resolved."));
    }

    private static async Task<ImportedProduct?> UpsertProductAsync(
        StoreDbContext database,
        NpgsqlTransaction transaction,
        ProductCsvGroup group,
        Guid categoryId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)database.Database.GetDbConnection();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO "Products"
                ("Id", "Name", "Sku", "SkuNormalized", "NameNormalized", "Description", "SearchText",
                 "CategoryId", "Price", "Stock", "WeightKg", "IsDeleted", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES
                (@id, @name, @sku, @skuNormalized, @nameNormalized, @description, @searchText,
                 @categoryId, @price, @stock, @weight, FALSE, @now, @now)
            ON CONFLICT ("SkuNormalized") DO UPDATE
            SET "Stock" = "Products"."Stock" + EXCLUDED."Stock",
                "UpdatedAtUtc" = EXCLUDED."UpdatedAtUtc"
            WHERE NOT "Products"."IsDeleted"
              AND "Products"."Stock" <= 2147483647 - EXCLUDED."Stock"
            RETURNING "Id", (xmax = 0), "IsDeleted", "Name", "Description", "Price", "Stock"
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("name", group.First.Name.Trim());
        command.Parameters.AddWithValue("sku", group.First.Sku.Trim());
        command.Parameters.AddWithValue("skuNormalized", group.NormalizedSku);
        command.Parameters.AddWithValue("nameNormalized", ProductTextNormalizer.Normalize(group.First.Name));
        command.Parameters.AddWithValue(
            "description",
            group.First.Description.Trim());
        command.Parameters.AddWithValue(
            "searchText",
            ProductTextNormalizer.BuildSearchText(group.First.Name, group.First.Sku, group.First.Description));
        command.Parameters.AddWithValue("categoryId", categoryId);
        command.Parameters.AddWithValue("price", group.First.Price);
        command.Parameters.AddWithValue("stock", group.Stock);
        command.Parameters.Add(new NpgsqlParameter("weight", NpgsqlDbType.Numeric)
        {
            Value = (object?)group.First.WeightKg ?? DBNull.Value
        });
        command.Parameters.AddWithValue("now", now);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ImportedProduct(
            reader.GetGuid(0),
            group.NormalizedSku,
            reader.GetBoolean(1),
            reader.GetBoolean(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetDecimal(5),
            reader.GetInt32(6));
    }

    private static async Task<bool> IsDeletedProductAsync(
        StoreDbContext database,
        string normalizedSku,
        CancellationToken cancellationToken) =>
        await database.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(product => product.SkuNormalized == normalizedSku)
            .Select(product => (bool?)product.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken) == true;

    private sealed record ImportedProduct(
        Guid Id,
        string Sku,
        bool Created,
        bool IsDeleted,
        string Name,
        string Description,
        decimal Price,
        int Stock);
}
