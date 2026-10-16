namespace Store.Application;

public sealed record ProductInput(
    string? Name,
    string? Sku,
    string? Description,
    string? Category,
    decimal Price,
    int Stock,
    decimal? WeightKg);
