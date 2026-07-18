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

/// <summary>
/// Where a hosted copy profile is in its startup lifecycle on the node currently running it. A profile is
/// <see cref="Warming"/> while the host loads reference data and runs its first resync — it is marked
/// Running in the database but is not yet mirroring orders across the destinations — and becomes
/// <see cref="Ready"/> once that first resync completes. A profile no node is actively hosting (never
/// started, hosted on another replica, or unhostable) is <see cref="NotHosted"/>.
/// </summary>
public enum CopyHostingPhase
{
    NotHosted = 0,
    Warming = 1,
    Ready = 2
}

/// <summary>
/// In-process registry of the live warming/ready phase of each hosted copy profile, written by the copy
/// host as it starts and read by the Web layer so the UI can show a "Starting" state until the engine is
/// actually ready to copy (rather than a green "Running" the instant the profile is started). Runtime-only
/// and node-local: a profile hosted on another replica reads back as <see cref="CopyHostingPhase.NotHosted"/>
/// there, so the caller falls back to the persisted status. Implementations MUST be thread-safe and never
/// throw — the host calls them on its hot path.
/// </summary>
public interface ICopyHostingStatus
{
    void MarkWarming(CopyProfileId profileId);
    void MarkReady(CopyProfileId profileId);
    void Clear(CopyProfileId profileId);
    CopyHostingPhase PhaseOf(CopyProfileId profileId);
}

/// <summary>No-op hosting status: the copy engine's default when no registry is wired (unit/stress tests).</summary>
public sealed class NullCopyHostingStatus : ICopyHostingStatus
{
    public static readonly NullCopyHostingStatus Instance = new();
    private NullCopyHostingStatus() { }
    public void MarkWarming(CopyProfileId profileId) { }
    public void MarkReady(CopyProfileId profileId) { }
    public void Clear(CopyProfileId profileId) { }
    public CopyHostingPhase PhaseOf(CopyProfileId profileId) => CopyHostingPhase.NotHosted;
}
