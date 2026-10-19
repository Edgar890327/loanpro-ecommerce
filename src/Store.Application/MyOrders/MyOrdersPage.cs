namespace Store.Application;

public sealed record MyOrdersPage(
    IReadOnlyList<MyOrderSummary> Items,
    int Page,
    int PageSize,
    int TotalCount);
