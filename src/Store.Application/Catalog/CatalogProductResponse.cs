namespace Store.Application;

public sealed record CatalogProductResponse(
    Guid Id,
    string Name,
    string Sku,
    string Description,
    string Category,
    decimal Price,
    int Stock,
    decimal? WeightKg);
