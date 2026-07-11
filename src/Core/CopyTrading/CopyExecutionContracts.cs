namespace Core.CopyTrading;

/// <summary>
/// What happened to a single copy attempt on one destination. Drives the execution-transparency report
/// (per-copy latency, slippage, fill vs failure) and, later, fee accrual and provider ratings.
/// </summary>
public enum CopyExecutionKind
{
    Opened = 0,
    Failed = 1,
    Skipped = 2,
    Closed = 3,
    Reconciled = 4
}

/// <summary>
/// An append-only fact emitted by the copy host for one copy attempt on one destination. A pure Core
/// value object (no infrastructure): the host produces it, an <see cref="ICopyEventSink"/> takes it, and
/// the sink implementation persists it out-of-band so the trading hot path never blocks on I/O.
/// </summary>
public sealed record CopyExecutionRecord(
    CopyProfileId ProfileId,
    long DestinationCtidTraderAccountId,
    long SourcePositionId,
    string Symbol,
    CopyExecutionKind Kind,
    bool IsBuy,
    long Volume,
    double MasterPrice,
    int? SlippagePoints,
    double LatencyMilliseconds,
    string? Reason,
    DateTimeOffset OccurredAt);

/// <summary>
/// Receives copy-execution facts from the host. Implementations MUST be non-blocking and never throw —
/// the caller is the trading hot path. The default <see cref="NullCopyEventSink"/> discards everything so
/// the engine runs unchanged when execution transparency is off (and in unit/stress/live tests).
/// </summary>
public interface ICopyEventSink
{
    void Record(CopyExecutionRecord record);
}

/// <summary>No-op sink: the copy engine's default when transparency is disabled. Records nothing.</summary>
public sealed class NullCopyEventSink : ICopyEventSink
{
    public static readonly NullCopyEventSink Instance = new();
    private NullCopyEventSink() { }
    public void Record(CopyExecutionRecord record) { }
}
