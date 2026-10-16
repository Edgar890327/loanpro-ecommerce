namespace Store.Application;

public sealed record ErrorLogQuery(
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    string? Level,
    string? CorrelationId,
    int Page,
    int PageSize);
