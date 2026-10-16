using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Application;
using Store.Domain;
using Store.Infrastructure;

namespace Store.Api;

public static class CatalogEndpoints
{
    public static RouteGroupBuilder MapCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/catalog").AllowAnonymous();
        group.MapGet("/", ListAsync);
        group.MapGet("/categories", CategoriesAsync);
        group.MapGet("/{id:guid}", GetAsync);
        return group;
    }

    private static async Task<IResult> ListAsync(
        StoreDbContext database,
        string? q = null,
        string? category = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        bool? inStock = null,
        string? sort = null,
        int page = 1,
        int pageSize = 12,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100 ||
            (long)(page - 1) * pageSize > int.MaxValue ||
            minPrice < 0 || maxPrice < 0 || minPrice > maxPrice)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["filters"] = ["Provide valid price bounds, a positive page, and a pageSize between 1 and 100."]
            });
        }

        CatalogQuery filters;
        try
        {
            filters = CatalogQueryBuilder.Build(q, category, minPrice, maxPrice, inStock, sort, page, pageSize);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["sort"] = ["Sort must be relevance, price, or name."]
            });
        }

        IQueryable<Product> products = database.Products.AsNoTracking();
        if (filters.Query is not null)
        {
            var pattern = $"%{EscapeLike(filters.Query)}%";
            products = products.Where(product =>
                EF.Functions.ILike(product.SearchText, pattern, "\\"));
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
                ? products.Where(product => product.Stock > 0)
                : products.Where(product => product.Stock == 0);
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
                product.Stock,
                product.WeightKg))
            .ToListAsync(cancellationToken);
        return Results.Ok(new CatalogPage(items, page, pageSize, totalCount));
    }

    private static async Task<IResult> CategoriesAsync(
        StoreDbContext database,
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
        var categories = counts
            .Select(count => new CatalogCategoryResponse(
                count.CategoryId,
                categoryNames[count.CategoryId],
                count.ProductCount))
            .OrderBy(category => category.Name)
            .ThenBy(category => category.Id)
            .ToArray();
        return Results.Ok(categories);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        StoreDbContext database,
        CancellationToken cancellationToken)
    {
        var product = await database.Products
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new CatalogProductResponse(
                item.Id,
                item.Name,
                item.Sku,
                item.Description,
                item.Category.Name,
                item.Price,
                item.Stock,
                item.WeightKg))
            .SingleOrDefaultAsync(cancellationToken);
        return product is null ? Results.NotFound() : Results.Ok(product);
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

}

public sealed record CatalogProductResponse(
    Guid Id,
    string Name,
    string Sku,
    string Description,
    string Category,
    decimal Price,
    int Stock,
    decimal? WeightKg);

public sealed record CatalogCategoryResponse(Guid Id, string Name, int ProductCount);

public sealed record CatalogPage(
    IReadOnlyCollection<CatalogProductResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
