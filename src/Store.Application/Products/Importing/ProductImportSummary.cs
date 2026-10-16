namespace Store.Application;

public sealed record ProductImportSummary(
    int CreatedProducts,
    long StockAdded,
    int Warnings,
    int Rejected,
    int ExactDuplicates);
