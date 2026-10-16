using Store.Application;

namespace Store.Api.Admin;

public sealed record ProductRequest(
    string? Name,
    string? Sku,
    string? Description,
    string? Category,
    decimal Price,
    int Stock,
    decimal? WeightKg,
    uint? Version)
{
    public ProductInput ToInput() =>
        new(Name, Sku, Description, Category, Price, Stock, WeightKg);
}
