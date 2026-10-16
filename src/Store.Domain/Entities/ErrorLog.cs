namespace Store.Domain;

public sealed class ErrorLog
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public DateTime TimestampUtc { get; init; }

    public string Level { get; init; } = string.Empty;

    public string CorrelationId { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string ExceptionType { get; init; } = string.Empty;

    public string? StackTrace { get; init; }

    public string HttpMethod { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public int StatusCode { get; init; }

    public Guid? OrderId { get; init; }

    public string? PaymentProviderKey { get; init; }
}
