namespace Store.Application;

public sealed record CatalogPage(
    IReadOnlyCollection<CatalogProductResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
