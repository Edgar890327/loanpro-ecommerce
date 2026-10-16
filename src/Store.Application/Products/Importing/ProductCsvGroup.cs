namespace Store.Application;

public sealed record ProductCsvGroup(
    ProductCsvRow First,
    string NormalizedSku,
    int Stock,
    IReadOnlyList<ProductCsvRow> Rows);
