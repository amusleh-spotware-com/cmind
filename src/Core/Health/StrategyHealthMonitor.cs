using System.Globalization;
using Core.Quant;

namespace Core.Health;

/// <summary>
/// Assesses alpha decay from a strategy's return series: compares the recent half's Sharpe to the
/// earlier half's and locates the largest mean-shift (CUSUM change-point). Pure and deterministic.
/// </summary>
public interface IStrategyHealthMonitor
{
    StrategyHealthReport Assess(ReturnSeries returns);
}

public sealed class StrategyHealthMonitor : IStrategyHealthMonitor
{
    private const int MinObservations = 8;      // below this we cannot split into meaningful halves
    private const double DecayedRatio = 0.25;   // recent Sharpe below a quarter of earlier → decayed
    private const double DegradingRatio = 0.60; // recent Sharpe below 60% of earlier → degrading
    private const double MeaningfulEarlierSharpe = 0.05;

    public StrategyHealthReport Assess(ReturnSeries returns)
    {
        ArgumentNullException.ThrowIfNull(returns);

        var values = returns.Values;
        var n = values.Count;
        if (n < MinObservations)
            return new StrategyHealthReport(StrategyHealth.Unknown, 0, 0, null, n,
                "Not enough history yet to judge whether the edge is holding.");

        var mid = n / 2;
        var earlier = SharpeOf(values, 0, mid);
        var recent = SharpeOf(values, mid, n);
        var changePoint = ChangePoint(values);

        var health = Classify(earlier, recent);
        return new StrategyHealthReport(health, earlier, recent, changePoint, n,
            BuildRationale(health, earlier, recent, changePoint));
    }

    private static StrategyHealth Classify(double earlier, double recent)
    {
        if (earlier <= MeaningfulEarlierSharpe)
            return recent >= earlier ? StrategyHealth.Healthy : StrategyHealth.Degrading;

        if (recent <= 0.0 || recent < earlier * DecayedRatio) return StrategyHealth.Decayed;
        if (recent < earlier * DegradingRatio) return StrategyHealth.Degrading;
        return StrategyHealth.Healthy;
    }

    private static double SharpeOf(IReadOnlyList<double> values, int start, int end)
    {
        var count = end - start;
        if (count < 2) return 0.0;
        var mean = 0.0;
        for (var i = start; i < end; i++) mean += values[i];
        mean /= count;
        var variance = 0.0;
        for (var i = start; i < end; i++)
        {
            var d = values[i] - mean;
            variance += d * d;
        }
        variance /= count;
        var std = Math.Sqrt(variance);
        return std > 0.0 ? mean / std : 0.0;
    }

    // CUSUM: the index at which the standardized cumulative deviation from the overall mean peaks in
    // magnitude — the most likely location of a mean shift. Null when the series has no dispersion.
    private static int? ChangePoint(IReadOnlyList<double> values)
    {
        var n = values.Count;
        var mean = 0.0;
        for (var i = 0; i < n; i++) mean += values[i];
        mean /= n;
        var variance = 0.0;
        for (var i = 0; i < n; i++)
        {
            var d = values[i] - mean;
            variance += d * d;
        }
        variance /= n;
        var std = Math.Sqrt(variance);
        if (std <= 0.0) return null;

        var cumulative = 0.0;
        var maxAbs = 0.0;
        int? at = null;
        for (var i = 0; i < n; i++)
        {
            cumulative += (values[i] - mean) / std;
            var abs = Math.Abs(cumulative);
            if (abs > maxAbs)
            {
                maxAbs = abs;
                at = i;
            }
        }
        // Only report a change-point that is statistically notable (deviation beyond √n scale).
        return maxAbs >= Math.Sqrt(n) ? at : null;
    }

    private static string BuildRationale(StrategyHealth health, double earlier, double recent, int? changePoint)
    {
        var cp = changePoint is { } c ? $" A regime shift is most evident around observation {c}." : string.Empty;
        var meaning = health switch
        {
            StrategyHealth.Decayed => "the edge has effectively disappeared in the recent window — consider pausing.",
            StrategyHealth.Degrading => "recent performance is materially weaker than the earlier record — watch closely.",
            StrategyHealth.Healthy => "recent performance is in line with the earlier record.",
            _ => "not enough history to judge.",
        };
        return string.Format(
            CultureInfo.InvariantCulture,
            "Earlier Sharpe {0:0.000} vs recent Sharpe {1:0.000}: {2}{3}",
            earlier, recent, meaning, cp);
    }
}
