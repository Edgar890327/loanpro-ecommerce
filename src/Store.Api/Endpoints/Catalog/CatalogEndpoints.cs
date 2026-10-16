using Store.Application;

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
        IProductCatalogReader catalog,
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

        CatalogQuery query;
        try
        {
            query = CatalogQueryBuilder.Build(q, category, minPrice, maxPrice, inStock, sort, page, pageSize);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["sort"] = ["Sort must be relevance, price, or name."]
            });
        }

        return Results.Ok(await catalog.ListAsync(query, cancellationToken));
    }

    private static async Task<IResult> CategoriesAsync(
        IProductCatalogReader catalog,
        CancellationToken cancellationToken) =>
        Results.Ok(await catalog.GetCategoriesAsync(cancellationToken));

    private static async Task<IResult> GetAsync(
        Guid id,
        IProductCatalogReader catalog,
        CancellationToken cancellationToken)
    {
        var product = await catalog.GetAsync(id, cancellationToken);
        return product is null ? Results.NotFound() : Results.Ok(product);
    }
}
