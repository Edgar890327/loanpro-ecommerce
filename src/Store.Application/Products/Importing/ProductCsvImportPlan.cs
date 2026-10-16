namespace Store.Application;

public sealed record ProductCsvImportPlan(
    IReadOnlyList<ProductCsvGroup> Groups,
    int ExactDuplicates);
