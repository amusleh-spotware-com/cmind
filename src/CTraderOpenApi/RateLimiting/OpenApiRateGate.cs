namespace CTraderOpenApi.RateLimiting;

/// <summary>
/// Per-message-type outbound pacer for one Open API connection: one <see cref="TokenBucket"/> per
/// <see cref="OpenApiRateCategory"/>. Consulted before each send in the (single-reader) send pump, so FIFO
/// order is preserved. A historical-data request draws from both its own stricter bucket and the general
/// bucket (mirroring the cTrader docs); heartbeat/handshake are exempt so keep-alive is never delayed.
/// </summary>
public sealed class OpenApiRateGate
{
    private readonly TokenBucket _general;
    private readonly TokenBucket _historical;

    public OpenApiRateGate(IReadOnlyDictionary<OpenApiRateCategory, int> limits, TimeProvider time)
    {
        _general = new TokenBucket(RateFor(limits, OpenApiRateCategory.General), time);
        _historical = new TokenBucket(RateFor(limits, OpenApiRateCategory.HistoricalData), time);
    }

    public async ValueTask AcquireAsync(uint payloadType, CancellationToken ct)
    {
        switch (OpenApiRateLimits.Classify(payloadType))
        {
            case OpenApiRateCategory.Exempt:
                return;
            case OpenApiRateCategory.HistoricalData:
                await _general.WaitAsync(ct);
                await _historical.WaitAsync(ct);
                return;
            default:
                await _general.WaitAsync(ct);
                return;
        }
    }

    // Refill on reconnect so a fresh connection does not instantly burst a full bucket and re-trip the cap.
    public void Reset()
    {
        _general.Reset();
        _historical.Reset();
    }

    private static int RateFor(IReadOnlyDictionary<OpenApiRateCategory, int> limits, OpenApiRateCategory category)
        => limits.TryGetValue(category, out var rate) ? rate : 0;
}
