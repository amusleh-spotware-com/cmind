using CTraderOpenApi.RateLimiting;

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

    /// <summary>
    /// Per-message-type outbound rate caps (messages/sec per connection), matching the cTrader Open API
    /// docs. A category set to <c>0</c> is unlimited. Defaults stay just under the published caps; a
    /// white-label broker with a negotiated higher limit raises a category or sets it <c>0</c> to disable.
    /// </summary>
    public IReadOnlyDictionary<OpenApiRateCategory, int> RateLimits { get; init; } =
        new Dictionary<OpenApiRateCategory, int>
        {
            [OpenApiRateCategory.General] = 45,
            [OpenApiRateCategory.HistoricalData] = 5
        };
}
