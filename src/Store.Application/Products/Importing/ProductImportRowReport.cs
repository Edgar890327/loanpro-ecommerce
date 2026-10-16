namespace Store.Application;

public sealed record ProductImportRowReport(int RowNumber, string Status, IReadOnlyList<string> Messages);
