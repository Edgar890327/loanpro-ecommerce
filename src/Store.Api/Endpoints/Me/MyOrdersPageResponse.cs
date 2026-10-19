namespace Store.Api.Me;

public sealed record MyOrdersPageResponse(
    IReadOnlyList<MyOrderSummaryResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
