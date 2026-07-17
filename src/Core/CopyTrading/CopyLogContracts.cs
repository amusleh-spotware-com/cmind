namespace Core.CopyTrading;

/// <summary>
/// Write side of the live copy-profile log. A running <c>CopyEngineHost</c> appends human-readable activity
/// lines (engine started, source open/close, per-destination placed/skipped/failed) for one profile so the
/// owner can watch a live tail — the copy-trading equivalent of a cBot run's console. Implementations MUST be
/// non-blocking and never throw: the caller is the trading hot path. The default <see cref="NullCopyLogSink"/>
/// discards everything so the engine runs unchanged when nobody is watching (and in unit/stress/live tests).
/// </summary>
public interface ICopyLogSink
{
    void Append(CopyProfileId profileId, string line);

    /// <summary>
    /// Signals that the profile's engine has stopped: the broker ends any open tails and releases the
    /// profile's buffered history so a stopped/deleted profile never leaks its ring for the process lifetime
    /// (the live-logs control is disabled once a profile is not running, so there is nothing left to view).
    /// </summary>
    void Complete(CopyProfileId profileId);
}

/// <summary>
/// Read side of the live copy-profile log. Yields the buffered recent history first, then new lines as they
/// happen, until the caller cancels (the viewer closes or the profile stops). Backed by an in-process broker
/// shared with <see cref="ICopyLogSink"/>.
/// </summary>
public interface ICopyLogFeed
{
    IAsyncEnumerable<string> TailAsync(CopyProfileId profileId, CancellationToken ct);
}

/// <summary>No-op sink: the copy engine's default when no live-log broker is wired. Records nothing.</summary>
public sealed class NullCopyLogSink : ICopyLogSink
{
    public static readonly NullCopyLogSink Instance = new();
    private NullCopyLogSink() { }
    public void Append(CopyProfileId profileId, string line) { }
    public void Complete(CopyProfileId profileId) { }
}
