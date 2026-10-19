namespace Store.Application;

public sealed record MyOrdersQuery(
    string? Q,
    string? Status,
    string? From,
    string? To,
    string? Sort,
    int Page = 1,
    int PageSize = MyOrdersFilterBuilder.DefaultPageSize);
