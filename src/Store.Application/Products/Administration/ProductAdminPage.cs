namespace Store.Application;

public sealed record ProductAdminPage(
    IReadOnlyCollection<ProductAdminView> Items,
    int Page,
    int PageSize,
    int TotalCount);
