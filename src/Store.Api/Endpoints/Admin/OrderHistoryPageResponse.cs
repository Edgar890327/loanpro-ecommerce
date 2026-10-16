namespace Store.Api.Admin;

public sealed record OrderHistoryPageResponse(
    IReadOnlyList<OrderHistoryResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
