namespace Store.Infrastructure.ErrorLogging;

public sealed class ErrorLogOptions
{
    public const string SectionName = "ErrorLogging";

    public int BatchSize { get; set; } = 50;

    public int QueueCapacity { get; set; } = 1000;

    public int RetentionDays { get; set; } = 30;

    public TimeSpan BatchInterval { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan RetentionInterval { get; set; } = TimeSpan.FromHours(24);
}
