using System.Globalization;

namespace Core.Quant;

/// <summary>
/// Computes the Probabilistic Sharpe Ratio (Bailey &amp; López de Prado 2012), a trial-count Deflated
/// Sharpe Ratio via the False Strategy Theorem, the return t-statistic, and a robustness verdict.
/// Pure and deterministic. When only a trial *count* is known (not the full set of trial Sharpes) the
/// deflation uses the Sharpe-estimator variance as the trial-dispersion proxy — a standard, conservative
/// simplification; the full grid path (Combinatorially-Purged CV / PBO) arrives with the native optimizer.
/// </summary>
public sealed class BacktestIntegrityAnalyzer : IBacktestIntegrityAnalyzer
{
    // Verdict thresholds: Deflated/Probabilistic Sharpe confidence + Harvey et al. t ≥ 3.0.
    private const double RobustDeflated = 0.95;
    private const double RobustProbabilistic = 0.95;
    private const double RobustTStat = 3.0;
    private const double OverfitDeflated = 0.90;

    // Floor for a degenerate Sharpe-estimator variance (pathological skew/kurtosis). Small but well above
    // subnormal so the z-score saturates cleanly instead of exploding to a NaN-adjacent value.
    private const double MinVarianceDenominator = 1e-12;

    public BacktestIntegrityReport Analyze(
        ReturnSeries returns,
        TrialCount trials,
        double benchmarkSharpe = 0.0,
        double periodsPerYear = 252.0)
    {
        ArgumentNullException.ThrowIfNull(returns);

        // Guard at the domain boundary so no caller can inject a non-finite benchmark or a non-positive
        // annualization factor: a non-finite benchmark would poison every probability; fall back to defaults.
        if (!double.IsFinite(benchmarkSharpe)) benchmarkSharpe = 0.0;
        if (!(periodsPerYear > 0.0)) periodsPerYear = 252.0;

        var n = returns.Count;
        var sr = returns.Sharpe;
        var skew = returns.Skewness;
        var kurt = returns.Kurtosis;

        var estimatorVariance = SharpeEstimatorVariance(sr, skew, kurt, n);
        var psr = ProbabilisticSharpe(sr, benchmarkSharpe, estimatorVariance);

        var deflatedBenchmark = DeflatedBenchmarkSharpe(trials.Value, estimatorVariance);
        var dsr = ProbabilisticSharpe(sr, deflatedBenchmark, estimatorVariance);

        var tStat = sr * Math.Sqrt(n);
        var verdict = Classify(dsr, psr, tStat);

        return new BacktestIntegrityReport(
            verdict,
            sr,
            returns.AnnualizedSharpe(periodsPerYear),
            new Probability(psr),
            new Probability(dsr),
            tStat,
            skew,
            kurt,
            n,
            trials.Value,
            ProbabilityOfBacktestOverfitting: null,
            BuildRationale(verdict, sr, psr, dsr, tStat, trials.Value, n));
    }

    // Variance of the Sharpe-ratio estimator (Bailey & López de Prado 2012), accounting for skew/kurtosis.
    private static double SharpeEstimatorVariance(double sr, double skew, double kurt, int n)
    {
        var denom = 1.0 - skew * sr + (kurt - 1.0) / 4.0 * sr * sr;
        if (denom < MinVarianceDenominator) denom = MinVarianceDenominator; // extreme skew/heavy tails: keep it finite
        return denom / (n - 1);
    }

    private static double ProbabilisticSharpe(double sr, double benchmark, double estimatorVariance)
    {
        if (estimatorVariance <= 0.0) return sr > benchmark ? 1.0 : 0.0;
        var z = (sr - benchmark) / Math.Sqrt(estimatorVariance);
        return Clamp01(QuantMath.NormalCdf(z));
    }

    // Expected maximum Sharpe under the null across `trials` independent tries (False Strategy Theorem).
    private static double DeflatedBenchmarkSharpe(int trials, double estimatorVariance)
    {
        if (trials <= 1) return 0.0; // no selection took place → no deflation → DSR collapses to PSR(0)
        var sigma = Math.Sqrt(estimatorVariance);
        var z1 = QuantMath.NormalInverseCdf(1.0 - 1.0 / trials);
        var z2 = QuantMath.NormalInverseCdf(1.0 - 1.0 / (trials * Math.E));
        return sigma * ((1.0 - QuantMath.EulerMascheroni) * z1 + QuantMath.EulerMascheroni * z2);
    }

    private static Verdict Classify(double dsr, double psr, double tStat)
    {
        if (dsr < OverfitDeflated) return Verdict.Overfit;
        if (dsr >= RobustDeflated && psr >= RobustProbabilistic && Math.Abs(tStat) >= RobustTStat)
            return Verdict.Robust;
        return Verdict.Fragile;
    }

    private static string BuildRationale(Verdict verdict, double sr, double psr, double dsr, double tStat, int trials, int n)
    {
        var meaning = verdict switch
        {
            Verdict.Robust =>
                "the edge survives correction for the number of configurations tried — deflated confidence is high.",
            Verdict.Overfit =>
                "after correcting for the configurations tried, the deflated confidence is low — this result is most likely an artefact of selection bias, not a real edge.",
            _ =>
                "statistically alive but not convincingly so once the trials are accounted for — do not size up on this alone.",
        };
        return string.Format(
            CultureInfo.InvariantCulture,
            "Sharpe {0:0.000} over {1} periods across {2} trial(s). Probabilistic Sharpe {3:0.0}% (confidence the true Sharpe beats the benchmark); Deflated Sharpe {4:0.0}% after correcting for {2} trial(s); t-stat {5:0.00}. Verdict: {6} — {7}",
            sr, n, trials, psr * 100.0, dsr * 100.0, tStat, verdict, meaning);
    }

    private static double Clamp01(double v) => v < 0.0 ? 0.0 : v > 1.0 ? 1.0 : v;
}
