namespace Store.Infrastructure.Orders;

public sealed class ReservationOptions
{
    public TimeSpan TimeToLive { get; init; } = TimeSpan.FromMinutes(15);

    public TimeSpan ScanInterval { get; init; } = TimeSpan.FromSeconds(15);

    public int BatchSize { get; init; } = 100;
}
