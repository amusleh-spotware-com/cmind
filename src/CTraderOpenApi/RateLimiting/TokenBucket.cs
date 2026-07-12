namespace CTraderOpenApi.RateLimiting;

/// <summary>
/// A continuous-refill token bucket paced by a <see cref="TimeProvider"/> (never the wall clock, so it is
/// deterministically testable). One bucket paces one <see cref="OpenApiRateCategory"/>. A rate of <c>0</c>
/// means unlimited — <see cref="WaitAsync"/> returns immediately with no allocation or delay. The Open API
/// send pump is single-reader, so a bucket is consulted from one thread at a time (no internal locking).
/// </summary>
public sealed class TokenBucket
{
    private readonly double _ratePerSecond;
    private readonly double _capacity;
    private readonly TimeProvider _time;
    private double _tokens;
    private long _lastTimestamp;

    public TokenBucket(int ratePerSecond, TimeProvider time)
    {
        _ratePerSecond = ratePerSecond;
        _capacity = Math.Max(1, ratePerSecond);
        _time = time;
        _tokens = _capacity;
        _lastTimestamp = time.GetTimestamp();
    }

    public bool Unlimited => _ratePerSecond <= 0;

    public async ValueTask WaitAsync(CancellationToken ct)
    {
        if (Unlimited) return;

        while (true)
        {
            Refill();
            if (_tokens >= 1)
            {
                _tokens -= 1;
                return;
            }

            var deficit = 1 - _tokens;
            await Task.Delay(TimeSpan.FromSeconds(deficit / _ratePerSecond), _time, ct);
        }
    }

    // Refill on reconnect so a fresh connection does not instantly burst a full bucket and re-trip the cap.
    public void Reset()
    {
        _tokens = _capacity;
        _lastTimestamp = _time.GetTimestamp();
    }

    private void Refill()
    {
        var now = _time.GetTimestamp();
        var elapsedSeconds = _time.GetElapsedTime(_lastTimestamp, now).TotalSeconds;
        _lastTimestamp = now;
        _tokens = Math.Min(_capacity, _tokens + elapsedSeconds * _ratePerSecond);
    }
}
