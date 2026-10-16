namespace Store.Api.Admin;

public sealed record ProductResponse(
    Guid Id,
    string Name,
    string Sku,
    string Description,
    string Category,
    decimal Price,
    int Stock,
    int AvailableStock,
    decimal? WeightKg,
    bool IsDeleted,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    uint Version)
{
    public static ProductResponse From(Store.Application.ProductAdminView product) => new(
        product.Id,
        product.Name,
        product.Sku,
        product.Description,
        product.Category,
        product.Price,
        product.Stock,
        product.AvailableStock,
        product.WeightKg,
        product.IsDeleted,
        product.CreatedAtUtc,
        product.UpdatedAtUtc,
        product.Version);
}
