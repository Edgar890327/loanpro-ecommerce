namespace Store.Application;

public sealed record ProductImportResult(
    ProductImportReport Report,
    bool ReimportConfirmationRequired);
