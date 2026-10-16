namespace Store.Application;

public sealed record CatalogQuery(
    string? Query,
    string? Category,
    decimal? MinPrice,
    decimal? MaxPrice,
    bool? InStock,
    CatalogSort Sort,
    int Page,
    int PageSize);
