using Store.Domain;

namespace Store.Application;

public sealed record ProductAdminView(
    Guid Id,
    string Name,
    string Sku,
    string Description,
    string Category,
    decimal Price,
    int Stock,
    decimal? WeightKg,
    bool IsDeleted,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    uint Version)
{
    public static ProductAdminView From(Product product, uint version) => new(
        product.Id,
        product.Name,
        product.Sku,
        product.Description,
        product.Category.Name,
        product.Price,
        product.Stock,
        product.WeightKg,
        product.IsDeleted,
        product.CreatedAtUtc,
        product.UpdatedAtUtc,
        version);
}
