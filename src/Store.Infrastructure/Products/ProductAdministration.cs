using Microsoft.EntityFrameworkCore;
using Npgsql;
using Store.Application;
using Store.Domain;

namespace Store.Infrastructure.Products;

public sealed class ProductAdministration(
    StoreDbContext database,
    TimeProvider timeProvider) : IProductAdministration
{
    public async Task<ProductAdminResult> CreateAsync(ProductInput input, CancellationToken cancellationToken)
    {
        var category = await FindOrCreateCategoryAsync(database, input.Category!, cancellationToken);
        var product = new Product(
            input.Name!,
            input.Sku!,
            input.Description ?? string.Empty,
            category,
            input.Price,
            input.Stock,
            input.WeightKg,
            timeProvider.GetUtcNow().UtcDateTime);
        database.Products.Add(product);
        try
        {
            await SaveProductAsync(database, product, category, input, product.UpdatedAtUtc, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueSkuViolation(exception))
        {
            return new ProductAdminResult(ProductAdminOutcome.DuplicateSku, null);
        }

        await database.Entry(product).ReloadAsync(cancellationToken);
        var version = database.Entry(product).Property<uint>("xmin").CurrentValue;
        return new ProductAdminResult(
            ProductAdminOutcome.Succeeded,
            ProductAdminView.From(product, version));
    }

    public async Task<ProductAdminPage> ListAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken)
    {
        IQueryable<Product> products = database.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchPattern = $"%{EscapeLike(search.Trim())}%";
            products = products.Where(product =>
                EF.Functions.ILike(product.Name, searchPattern) ||
                EF.Functions.ILike(product.Sku, searchPattern) ||
                EF.Functions.ILike(product.Category.Name, searchPattern));
        }

        var totalCount = await products.CountAsync(cancellationToken);
        var rows = await products
            .OrderBy(product => product.Name)
            .ThenBy(product => product.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(product => new ProductAdminView(
                product.Id,
                product.Name,
                product.Sku,
                product.Description,
                product.Category.Name,
                product.Price,
                product.Stock,
                product.Available,
                product.WeightKg,
                product.IsDeleted,
                product.CreatedAtUtc,
                product.UpdatedAtUtc,
                EF.Property<uint>(product, "xmin")))
            .ToListAsync(cancellationToken);
        return new ProductAdminPage(rows, page, pageSize, totalCount);
    }

    public Task<ProductAdminView?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        database.Products
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new ProductAdminView(
                item.Id,
                item.Name,
                item.Sku,
                item.Description,
                item.Category.Name,
                item.Price,
                item.Stock,
                item.Available,
                item.WeightKg,
                item.IsDeleted,
                item.CreatedAtUtc,
                item.UpdatedAtUtc,
                EF.Property<uint>(item, "xmin")))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ProductAdminResult> UpdateAsync(
        Guid id,
        ProductInput input,
        uint version,
        CancellationToken cancellationToken)
    {
        var product = await database.Products
            .Include(item => item.Category)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (product is null)
        {
            return new ProductAdminResult(ProductAdminOutcome.NotFound, null);
        }

        if (input.Stock < product.Reserved)
        {
            return new ProductAdminResult(ProductAdminOutcome.ReservedStockConflict, null);
        }

        database.Entry(product).Property<uint>("xmin").OriginalValue = version;
        var category = await FindOrCreateCategoryAsync(database, input.Category!, cancellationToken);
        product.Update(
            input.Name!,
            input.Sku!,
            input.Description ?? string.Empty,
            category,
            input.Price,
            input.Stock,
            input.WeightKg,
            timeProvider.GetUtcNow().UtcDateTime);
        try
        {
            await SaveProductAsync(database, product, category, input, product.UpdatedAtUtc, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ProductAdminResult(ProductAdminOutcome.ConcurrencyConflict, null);
        }
        catch (DbUpdateException exception) when (IsUniqueSkuViolation(exception))
        {
            return new ProductAdminResult(ProductAdminOutcome.DuplicateSku, null);
        }

        await database.Entry(product).ReloadAsync(cancellationToken);
        return new ProductAdminResult(
            ProductAdminOutcome.Succeeded,
            ProductAdminView.From(product, database.Entry(product).Property<uint>("xmin").CurrentValue));
    }

    public async Task<ProductAdminOutcome> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await database.Products.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (product is null)
        {
            return ProductAdminOutcome.NotFound;
        }

        product.Delete(timeProvider.GetUtcNow().UtcDateTime);
        await database.SaveChangesAsync(cancellationToken);
        return ProductAdminOutcome.Succeeded;
    }

    private static async Task<Category> FindOrCreateCategoryAsync(
        StoreDbContext context,
        string categoryName,
        CancellationToken cancellationToken)
    {
        var normalizedName = categoryName.Trim().ToUpperInvariant();
        var category = await context.Categories
            .SingleOrDefaultAsync(item => item.NormalizedName == normalizedName, cancellationToken);
        if (category is not null)
        {
            return category;
        }

        category = new Category(categoryName);
        context.Categories.Add(category);
        return category;
    }

    private static async Task SaveProductAsync(
        StoreDbContext context,
        Product product,
        Category category,
        ProductInput input,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueCategoryViolation(exception))
        {
            context.Entry(category).State = EntityState.Detached;
            var existingCategory = await context.Categories
                .SingleAsync(item => item.NormalizedName == category.NormalizedName, cancellationToken);
            product.Update(
                input.Name!,
                input.Sku!,
                input.Description ?? string.Empty,
                existingCategory,
                input.Price,
                input.Stock,
                input.WeightKg,
                nowUtc);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static bool IsUniqueSkuViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Products_SkuNormalized"
        };

    private static bool IsUniqueCategoryViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Categories_NormalizedName"
        };

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
