namespace CTraderOpenApi;

public sealed class BackoffPolicy(TimeSpan initial, TimeSpan max, double factor, Func<double>? jitter = null)
{
    private readonly Func<double> _jitter = jitter ?? Random.Shared.NextDouble;
    private int _attempt;

    public void Reset() => _attempt = 0;

    public TimeSpan NextDelay()
    {
        var baseMs = Math.Min(initial.TotalMilliseconds * Math.Pow(factor, _attempt), max.TotalMilliseconds);
        _attempt++;
        var scale = 0.5 + (0.5 * Math.Clamp(_jitter(), 0d, 1d));
        return TimeSpan.FromMilliseconds(baseMs * scale);
    }
}
