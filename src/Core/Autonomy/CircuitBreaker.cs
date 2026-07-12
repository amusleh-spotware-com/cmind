using Core.Constants;
using Core.Domain;

namespace Core.Autonomy;

/// <summary>A one-time acceptance of the autonomous-trading risk disclaimer, versioned so a changed
/// disclaimer forces re-consent. Consent is legally-required and is not per-trade approval.</summary>
public readonly record struct DisclaimerConsent
{
    public int Version { get; }
    public DateTimeOffset AcceptedAt { get; }

    public DisclaimerConsent(int version, DateTimeOffset acceptedAt)
    {
        if (version < 1 || acceptedAt == default) throw new DomainException(DomainErrors.DisclaimerConsentInvalid);
        Version = version;
        AcceptedAt = acceptedAt;
    }

    /// <summary>True when this consent covers the current disclaimer version.</summary>
    public bool IsCurrent(int currentVersion) => Version >= currentVersion;
}

/// <summary>The runtime health metrics the circuit breaker judges.</summary>
public sealed record BreakerMetrics(
    int ConsecutiveLosses,
    double DailyLossPercent,
    bool AiAvailable,
    bool HardGoalBreached);

/// <summary>The breaker's decision: whether autonomous trading must halt and why.</summary>
public sealed record BreakerDecision(bool Tripped, string? Reason)
{
    public static BreakerDecision Ok() => new(false, null);
    public static BreakerDecision Trip(string reason) => new(true, reason);
}

/// <summary>
/// Deterministic safety backstop for autonomous trading. It halts new risk when the loss streak or
/// daily loss breaches the envelope, when a hard performance goal is breached, or when the AI provider
/// is unavailable — a down or hallucinating model must never keep opening fresh positions. No LLM here.
/// </summary>
public static class CircuitBreaker
{
    public static BreakerDecision Evaluate(RiskEnvelope envelope, BreakerMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(metrics);

        if (!metrics.AiAvailable)
            return BreakerDecision.Trip("AI provider unavailable — halting new entries; open risk is still managed.");
        if (metrics.HardGoalBreached)
            return BreakerDecision.Trip("A hard performance goal was breached.");
        if (metrics.ConsecutiveLosses >= envelope.MaxConsecutiveLosses)
            return BreakerDecision.Trip($"Consecutive losses {metrics.ConsecutiveLosses} hit the limit {envelope.MaxConsecutiveLosses}.");
        if (metrics.DailyLossPercent >= envelope.MaxDailyLossPercent)
            return BreakerDecision.Trip($"Daily loss {metrics.DailyLossPercent:0.0}% hit the limit {envelope.MaxDailyLossPercent:0.0}%.");
        return BreakerDecision.Ok();
    }
}
