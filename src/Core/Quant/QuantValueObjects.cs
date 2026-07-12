using Core.Constants;
using Core.Domain;

namespace Core.Quant;

/// <summary>A probability in the closed interval [0, 1]. Self-validating; used for PSR, DSR and PBO.</summary>
public readonly record struct Probability
{
    public double Value { get; }

    public Probability(double value)
    {
        if (double.IsNaN(value) || value < 0.0 || value > 1.0)
            throw new DomainException(DomainErrors.ProbabilityOutOfRange);
        Value = value;
    }

    public double Percent => Value * 100.0;
    public override string ToString() => Value.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// The number of strategy configurations tried before selecting this result. One trial means no
/// selection took place; many trials inflate the best in-sample Sharpe and must deflate the verdict.
/// </summary>
public readonly record struct TrialCount
{
    public int Value { get; }

    public TrialCount(int value)
    {
        if (value < 1) throw new DomainException(DomainErrors.TrialCountInvalid);
        Value = value;
    }

    public static TrialCount Single => new(1);
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// How much a backtest result can be trusted once corrected for selection bias and overfitting.
/// </summary>
public enum Verdict
{
    /// <summary>Deflated significance holds — the edge survives the number of trials run.</summary>
    Robust,

    /// <summary>Statistically alive but not convincingly so — treat with caution, do not size up.</summary>
    Fragile,

    /// <summary>The result is most likely an artefact of overfitting / selection bias.</summary>
    Overfit
}
