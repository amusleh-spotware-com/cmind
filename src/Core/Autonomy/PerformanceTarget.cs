using Core.Constants;
using Core.Domain;

namespace Core.Autonomy;

public enum TargetMetric
{
    MaxDrawdown,
    ProfitFactor,
    WinRate,
    SharpeRatio,
    MonthlyReturn,
    MaxDailyLoss,
    Expectancy,
    MaxOpenPositions
}

public enum TargetComparator
{
    Below,
    AtMost,
    Above,
    AtLeast
}

public enum TargetEnforcement
{
    /// <summary>Steers reasoning only; does not halt.</summary>
    Soft,

    /// <summary>A guardrail: a breach trips the circuit breaker.</summary>
    Hard
}

public enum GoalStatus
{
    OnTrack,
    AtRisk,
    Breached
}

/// <summary>
/// A user-defined, measurable objective for an agent — e.g. "keep max drawdown below 4%" or "profit
/// factor at least 1.5". Hard targets are enforced as guardrails (a breach halts the agent); soft
/// targets steer its reasoning. Self-validating and deterministic.
/// </summary>
public sealed class PerformanceTarget
{
    private const double AtRiskMargin = 0.10; // within 10% of the threshold → AtRisk

    public PerformanceTarget(TargetMetric metric, TargetComparator comparator, double threshold, TargetEnforcement enforcement)
    {
        if (double.IsNaN(threshold) || double.IsInfinity(threshold)) throw new DomainException(DomainErrors.PerformanceTargetInvalid);
        switch (metric)
        {
            case TargetMetric.MaxDrawdown or TargetMetric.WinRate or TargetMetric.MaxDailyLoss when threshold is < 0 or > 100:
                throw new DomainException(DomainErrors.PerformanceTargetInvalid);
            case TargetMetric.ProfitFactor when threshold < 0:
                throw new DomainException(DomainErrors.PerformanceTargetInvalid);
            case TargetMetric.MaxOpenPositions when threshold < 0:
                throw new DomainException(DomainErrors.PerformanceTargetInvalid);
        }

        Metric = metric;
        Comparator = comparator;
        Threshold = threshold;
        Enforcement = enforcement;
    }

    public TargetMetric Metric { get; }
    public TargetComparator Comparator { get; }
    public double Threshold { get; }
    public TargetEnforcement Enforcement { get; }
    public bool IsHard => Enforcement == TargetEnforcement.Hard;

    /// <summary>Evaluates the current measured value of the metric against this target.</summary>
    public GoalStatus Evaluate(double actual)
    {
        var satisfied = Comparator switch
        {
            TargetComparator.Below => actual < Threshold,
            TargetComparator.AtMost => actual <= Threshold,
            TargetComparator.Above => actual > Threshold,
            TargetComparator.AtLeast => actual >= Threshold,
            _ => true
        };
        if (!satisfied) return GoalStatus.Breached;

        var denom = Math.Abs(Threshold) < double.Epsilon ? 1.0 : Math.Abs(Threshold);
        var margin = Comparator is TargetComparator.Below or TargetComparator.AtMost
            ? (Threshold - actual) / denom
            : (actual - Threshold) / denom;
        return margin < AtRiskMargin ? GoalStatus.AtRisk : GoalStatus.OnTrack;
    }
}
