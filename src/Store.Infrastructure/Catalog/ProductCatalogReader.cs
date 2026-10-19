using Microsoft.EntityFrameworkCore;
using Store.Application;
using Store.Domain;

namespace Store.Infrastructure.Catalog;

public sealed class ProductCatalogReader(StoreDbContext database) : IProductCatalogReader
{
    public async Task<CatalogPage> ListAsync(CatalogQuery filters, CancellationToken cancellationToken)
    {
        IQueryable<Product> products = database.Products.AsNoTracking();
        if (filters.Query is not null)
        {
            foreach (var term in filters.Query.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var pattern = $"%{EscapeLike(term)}%";
                products = products.Where(product =>
                    EF.Functions.ILike(product.SearchText, pattern, "\\"));
            }
        }

        if (filters.Category is not null)
        {
            var normalizedCategory = filters.Category.ToUpperInvariant();
            products = products.Where(product => product.Category.NormalizedName == normalizedCategory);
        }

        if (filters.MinPrice.HasValue)
        {
            products = products.Where(product => product.Price >= filters.MinPrice.Value);
        }

        if (filters.MaxPrice.HasValue)
        {
            products = products.Where(product => product.Price <= filters.MaxPrice.Value);
        }

        if (filters.InStock.HasValue)
        {
            products = filters.InStock.Value
                ? products.Where(product => product.Stock > product.Reserved)
                : products.Where(product => product.Stock == product.Reserved);
        }

        var totalCount = await products.CountAsync(cancellationToken);
        var items = await CatalogQueryBuilder.ApplySort(products, filters)
            .Skip((filters.Page - 1) * filters.PageSize)
            .Take(filters.PageSize)
            .Select(product => new CatalogProductResponse(
                product.Id,
                product.Name,
                product.Sku,
                product.Description,
                product.Category.Name,
                product.Price,
                product.Stock - product.Reserved,
                product.WeightKg))
            .ToListAsync(cancellationToken);
        return new CatalogPage(items, filters.Page, filters.PageSize, totalCount);
    }

    public async Task<IReadOnlyCollection<CatalogCategoryResponse>> GetCategoriesAsync(
        CancellationToken cancellationToken)
    {
        var counts = await database.Products
            .AsNoTracking()
            .GroupBy(product => product.CategoryId)
            .Select(group => new { CategoryId = group.Key, ProductCount = group.Count() })
            .ToListAsync(cancellationToken);
        var categoryIds = counts.Select(count => count.CategoryId).ToArray();
        var categoryNames = await database.Categories
            .AsNoTracking()
            .Where(category => categoryIds.Contains(category.Id))
            .Select(category => new { category.Id, category.Name })
            .ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);
        return counts
            .Select(count => new CatalogCategoryResponse(
                count.CategoryId,
                categoryNames[count.CategoryId],
                count.ProductCount))
            .OrderBy(category => category.Name)
            .ThenBy(category => category.Id)
            .ToArray();
    }

    public Task<CatalogProductResponse?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        database.Products
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new CatalogProductResponse(
                item.Id,
                item.Name,
                item.Sku,
                item.Description,
                item.Category.Name,
                item.Price,
                item.Stock - item.Reserved,
                item.WeightKg))
            .SingleOrDefaultAsync(cancellationToken);

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
