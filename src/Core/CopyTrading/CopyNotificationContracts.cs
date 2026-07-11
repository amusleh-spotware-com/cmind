namespace Core.CopyTrading;

/// <summary>A safety-relevant thing the copy host wants the profile owner to know about.</summary>
public enum CopyNotificationKind
{
    DestinationTripped = 0,        // G8: a destination's rejection budget was exhausted; new opens paused.
    AccountProtectionTriggered = 1, // ZuluGuard equity floor/ceiling breached; opens latched (SellOut liquidates).
    PropRuleBreached = 2,          // Prop daily-loss / trailing-drawdown breached; destination flattened + locked out.
    FlattenAll = 3,                // Panic flatten executed; every destination closed + locked.
    TokenInvalidated = 4           // A destination's access token was invalidated; awaiting rotation.
}

public enum CopyNotificationSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

/// <summary>
/// An operational notification emitted by the copy host for the profile owner. A pure Core value object:
/// the host produces it, an <see cref="ICopyNotificationSink"/> takes it, and the sink persists it
/// out-of-band (resolving the owner from the profile) so the trading hot path never blocks on I/O.
/// </summary>
public sealed record CopyNotificationRecord(
    CopyProfileId ProfileId,
    long? DestinationCtidTraderAccountId,
    CopyNotificationKind Kind,
    CopyNotificationSeverity Severity,
    string Message,
    DateTimeOffset OccurredAt);

/// <summary>
/// Receives operational notifications from the host. Implementations MUST be non-blocking and never throw
/// (the caller is the trading hot path). The default <see cref="NullCopyNotificationSink"/> discards
/// everything so the engine runs unchanged in unit/stress/live tests.
/// </summary>
public interface ICopyNotificationSink
{
    void Notify(CopyNotificationRecord record);
}

/// <summary>No-op sink: the copy engine's default. Records nothing.</summary>
public sealed class NullCopyNotificationSink : ICopyNotificationSink
{
    public static readonly NullCopyNotificationSink Instance = new();
    private NullCopyNotificationSink() { }
    public void Notify(CopyNotificationRecord record) { }
}
