using Core.Constants;
using Core.Domain;

namespace Core.Portfolio;

/// <summary>An annualized volatility target as a fraction (0.10 = 10% per year). Positive, bounded.</summary>
public readonly record struct VolatilityTarget
{
    public double Value { get; }

    public VolatilityTarget(double value)
    {
        if (double.IsNaN(value) || value <= 0.0 || value > 10.0)
            throw new DomainException(DomainErrors.VolatilityTargetInvalid);
        Value = value;
    }

    public double Percent => Value * 100.0;
}

/// <summary>
/// The fraction of full Kelly to apply (0 &lt; f ≤ 1). Full Kelly maximizes long-run growth but is famously
/// too aggressive; retail should stay at or below one half (fractional Kelly).
/// </summary>
public readonly record struct KellyFraction
{
    public double Value { get; }

    public KellyFraction(double value)
    {
        if (double.IsNaN(value) || value <= 0.0 || value > 1.0)
            throw new DomainException(DomainErrors.KellyFractionInvalid);
        Value = value;
    }

    public static KellyFraction Half => new(0.5);
}

/// <summary>Maximum gross exposure a sizing recommendation may return (e.g. 3 = up to 3× notional).</summary>
public readonly record struct LeverageCap
{
    public double Value { get; }

    public LeverageCap(double value)
    {
        if (double.IsNaN(value) || value <= 0.0 || value > 100.0)
            throw new DomainException(DomainErrors.LeverageCapInvalid);
        Value = value;
    }

    public static LeverageCap Default => new(3.0);
}
