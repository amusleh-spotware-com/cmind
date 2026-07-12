using Core.Constants;
using Core.Domain;

namespace Core.Quant;

/// <summary>
/// An immutable series of periodic returns (fractions, e.g. 0.01 = +1%). The unit of statistical
/// analysis for the Backtest Integrity Lab: self-validating (at least two finite observations) and
/// exposing the sample moments the Sharpe-based statistics need. All moments use population central
/// moments (ddof = 0); the estimator correction lives in the analyzer's (n-1) terms.
/// </summary>
public sealed class ReturnSeries
{
    private readonly double[] _values;

    private ReturnSeries(double[] values)
    {
        _values = values;
        Count = values.Length;
        Mean = ComputeMean(values);
        var (m2, m3, m4) = ComputeCentralMoments(values, Mean);
        Variance = m2;
        StandardDeviation = Math.Sqrt(m2);
        Skewness = m2 > 0 ? m3 / Math.Pow(m2, 1.5) : 0.0;
        Kurtosis = m2 > 0 ? m4 / (m2 * m2) : 0.0;
    }

    public int Count { get; }
    public double Mean { get; }
    public double Variance { get; }
    public double StandardDeviation { get; }

    /// <summary>Standardized third central moment (0 for a symmetric distribution).</summary>
    public double Skewness { get; }

    /// <summary>Standardized fourth central moment, non-excess (3 for a normal distribution).</summary>
    public double Kurtosis { get; }

    public IReadOnlyList<double> Values => _values;

    /// <summary>Per-observation Sharpe ratio (mean / standard deviation). Zero when the series is flat.</summary>
    public double Sharpe => StandardDeviation > 0 ? Mean / StandardDeviation : 0.0;

    /// <summary>Sharpe scaled to a horizon by the square-root-of-time rule (e.g. 252 for daily returns).</summary>
    public double AnnualizedSharpe(double periodsPerYear) => Sharpe * Math.Sqrt(periodsPerYear);

    /// <summary>Builds a series from raw periodic returns. Requires at least two finite values.</summary>
    public static ReturnSeries From(IReadOnlyList<double> returns)
    {
        ArgumentNullException.ThrowIfNull(returns);
        if (returns.Count < 2) throw new DomainException(DomainErrors.ReturnSeriesTooShort);
        var copy = new double[returns.Count];
        for (var i = 0; i < returns.Count; i++)
        {
            var v = returns[i];
            if (double.IsNaN(v) || double.IsInfinity(v)) throw new DomainException(DomainErrors.ReturnSeriesNotFinite);
            copy[i] = v;
        }
        return new ReturnSeries(copy);
    }

    /// <summary>
    /// Derives periodic returns from an equity/balance curve (consecutive simple returns). Needs at least
    /// three equity points to yield the two returns a series requires. Non-positive prior equity is skipped.
    /// </summary>
    public static ReturnSeries FromEquityCurve(IReadOnlyList<double> equity)
    {
        ArgumentNullException.ThrowIfNull(equity);
        var returns = new List<double>(Math.Max(0, equity.Count - 1));
        for (var i = 1; i < equity.Count; i++)
        {
            var prev = equity[i - 1];
            if (double.IsNaN(prev) || double.IsInfinity(prev) || prev <= 0.0) continue;
            var curr = equity[i];
            if (double.IsNaN(curr) || double.IsInfinity(curr)) continue;
            returns.Add((curr - prev) / prev);
        }
        return From(returns);
    }

    private static double ComputeMean(double[] values)
    {
        var sum = 0.0;
        foreach (var v in values) sum += v;
        return sum / values.Length;
    }

    private static (double M2, double M3, double M4) ComputeCentralMoments(double[] values, double mean)
    {
        double m2 = 0, m3 = 0, m4 = 0;
        foreach (var v in values)
        {
            var d = v - mean;
            var d2 = d * d;
            m2 += d2;
            m3 += d2 * d;
            m4 += d2 * d2;
        }
        var n = values.Length;
        return (m2 / n, m3 / n, m4 / n);
    }
}
