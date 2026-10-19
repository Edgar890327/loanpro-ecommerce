namespace Store.Infrastructure.E2e;

public sealed record E2eProductFixture(
    string Name,
    string Sku,
    string Description,
    string Category,
    decimal Price,
    int Stock,
    decimal? WeightKg);
