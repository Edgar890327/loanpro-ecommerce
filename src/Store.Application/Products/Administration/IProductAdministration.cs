namespace Store.Application;

public interface IProductAdministration
{
    Task<ProductAdminResult> CreateAsync(ProductInput input, CancellationToken cancellationToken);

    Task<ProductAdminPage> ListAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken);

    Task<ProductAdminView?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<ProductAdminResult> UpdateAsync(
        Guid id,
        ProductInput input,
        uint version,
        CancellationToken cancellationToken);

    Task<ProductAdminOutcome> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
