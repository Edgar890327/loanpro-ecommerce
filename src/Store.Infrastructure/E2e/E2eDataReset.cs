using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Store.Application;
using Store.Domain;

namespace Store.Infrastructure.E2e;

public sealed class E2eDataReset(
    IDbContextFactory<StoreDbContext> contextFactory,
    IPaymentProviderRegistry paymentProviders,
    IShippingStrategyRegistry shippingStrategies,
    TimeProvider timeProvider) : IE2eDataReset
{
    private const string FixtureResourceName = "Store.Infrastructure.E2e.e2e-products.json";

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        await using (var database = await contextFactory.CreateDbContextAsync(cancellationToken))
        await using (var transaction = await database.Database.BeginTransactionAsync(cancellationToken))
        {
            await database.Database.ExecuteSqlRawAsync(
                """
                TRUNCATE TABLE
                    "ErrorLog",
                    "StockMovements",
                    "OrderItems",
                    "Payments",
                    "Orders",
                    "ImportBatches",
                    "Products",
                    "Categories",
                    "PaymentProviderSettings",
                    "ShippingStrategySettings"
                CASCADE
                """,
                cancellationToken);

            var fixture = await ReadFixtureAsync(cancellationToken);
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var categories = new Dictionary<string, Category>(StringComparer.Ordinal);
            foreach (var product in fixture)
            {
                if (!categories.TryGetValue(product.Category, out var category))
                {
                    category = new Category(product.Category);
                    categories.Add(product.Category, category);
                    database.Categories.Add(category);
                }

                database.Products.Add(new Product(
                    product.Name,
                    product.Sku,
                    product.Description,
                    category,
                    product.Price,
                    product.Stock,
                    product.WeightKg,
                    now));
            }

            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await paymentProviders.EnsureInitializedAsync(cancellationToken);
        await shippingStrategies.EnsureInitializedAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<E2eProductFixture>> ReadFixtureAsync(
        CancellationToken cancellationToken)
    {
        await using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(FixtureResourceName)
            ?? throw new InvalidOperationException("The E2E product fixture is missing.");
        return await JsonSerializer.DeserializeAsync<List<E2eProductFixture>>(
                stream,
                cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("The E2E product fixture is invalid.");
    }
}
