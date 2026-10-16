namespace Store.Application;

public sealed record OrderHistoryPage(
    IReadOnlyList<OrderHistoryItem> Items,
    int Page,
    int PageSize,
    int TotalCount);
