namespace Store.Application;

public sealed record ProductImportReport(
    string FileHash,
    bool DryRun,
    bool ReimportDetected,
    DateTime? PreviousImportAtUtc,
    ProductImportSummary Summary,
    IReadOnlyList<ProductImportRowReport> Rows);
