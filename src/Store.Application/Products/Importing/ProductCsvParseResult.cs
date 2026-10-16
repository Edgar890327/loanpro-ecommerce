namespace Store.Application;

public sealed record ProductCsvParseResult(IReadOnlyList<ProductCsvRow> Rows);
