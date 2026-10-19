namespace Store.Application;

public sealed record MyOrdersFilter(
    Guid CustomerId,
    string? Status,
    string? QueryPattern,
    DateTime? FromUtc,
    DateTime? ToUtc,
    MyOrdersSort Sort,
    int Page,
    int PageSize)
{
    public int Offset => (Page - 1) * PageSize;
}
