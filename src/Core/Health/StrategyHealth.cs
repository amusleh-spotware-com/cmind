namespace Core.Health;

/// <summary>
/// The alpha-decay state of a live strategy: is the edge still there, fading, or gone? Adaptation beats
/// discovery — a decayed edge should be paused, not ridden.
/// </summary>
public enum StrategyHealth
{
    /// <summary>Not enough history to judge.</summary>
    Unknown,

    /// <summary>Recent performance is in line with (or better than) the earlier track record.</summary>
    Healthy,

    /// <summary>Recent performance is materially weaker than the earlier track record.</summary>
    Degrading,

    /// <summary>The edge has effectively disappeared in the recent window.</summary>
    Decayed
}

/// <summary>
/// The result of a strategy-health assessment: the recent vs earlier Sharpe, the detected change-point,
/// the verdict and a plain-English rationale. Deterministic.
/// </summary>
public sealed record StrategyHealthReport(
    StrategyHealth Health,
    double EarlierSharpe,
    double RecentSharpe,
    int? ChangePointIndex,
    int Observations,
    string Rationale);
