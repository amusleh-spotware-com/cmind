namespace CTraderOpenApi;

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Faulted
}

public sealed record OpenApiConnectionOptions
{
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan InboundWatchdogTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan BackoffInitial { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan BackoffMax { get; init; } = TimeSpan.FromSeconds(60);
    public double BackoffFactor { get; init; } = 2.0;
    public TimeSpan MaintenanceMinDelay { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan MaintenanceMaxDelay { get; init; } = TimeSpan.FromHours(6);
}
