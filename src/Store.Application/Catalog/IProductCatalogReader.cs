namespace Store.Application;

public interface IProductCatalogReader
{
    Task<CatalogPage> ListAsync(CatalogQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CatalogCategoryResponse>> GetCategoriesAsync(CancellationToken cancellationToken);

    Task<CatalogProductResponse?> GetAsync(Guid id, CancellationToken cancellationToken);
}
