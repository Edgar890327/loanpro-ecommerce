using Store.Domain;

namespace Store.Application;

public sealed record ErrorLogPage(
    IReadOnlyCollection<ErrorLog> Items,
    int Page,
    int PageSize,
    int TotalCount);
