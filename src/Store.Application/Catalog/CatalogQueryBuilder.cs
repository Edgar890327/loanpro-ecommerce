using System.Linq.Expressions;
using Store.Domain;

namespace Store.Application;

public static class CatalogQueryBuilder
{
    public static CatalogQuery Build(
        string? query,
        string? category,
        decimal? minPrice,
        decimal? maxPrice,
        bool? inStock,
        string? sort,
        int page,
        int pageSize)
    {
        var normalizedSort = sort?.Trim().ToLowerInvariant() switch
        {
            null or "" or "relevance" => CatalogSort.Relevance,
            "price" => CatalogSort.Price,
            "name" => CatalogSort.Name,
            _ => throw new ArgumentOutOfRangeException(nameof(sort), "Sort must be relevance, price, or name.")
        };

        return new CatalogQuery(
            string.IsNullOrWhiteSpace(query) ? null : ProductTextNormalizer.Normalize(query),
            string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            minPrice,
            maxPrice,
            inStock,
            normalizedSort,
            page,
            pageSize);
    }

    public static Expression<Func<Product, int>> BuildRelevanceRank(string query) =>
        product =>
            product.NameNormalized == query || product.SkuNormalized == query
                ? 0
                : product.NameNormalized.StartsWith(query) || product.SkuNormalized.StartsWith(query)
                    ? 1
                    : 2;

    public static IOrderedQueryable<Product> ApplySort(IQueryable<Product> products, CatalogQuery query) =>
        query.Sort switch
        {
            CatalogSort.Price => products.OrderBy(product => product.Price)
                .ThenBy(product => product.Name)
                .ThenBy(product => product.Id),
            CatalogSort.Name => products.OrderBy(product => product.Name)
                .ThenBy(product => product.Id),
            _ when query.Query is not null => products
                .OrderBy(BuildRelevanceRank(query.Query))
                .ThenBy(product => product.Name)
                .ThenBy(product => product.Id),
            _ => products.OrderBy(product => product.Name).ThenBy(product => product.Id)
        };
}
