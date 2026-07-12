using System.Globalization;
using Core.Quant;

namespace Core.Regimes;

/// <summary>
/// Labels a return series into volatility regimes (by trailing-volatility terciles), reports the
/// strategy's performance within each, and estimates the Hurst exponent via rescaled-range analysis.
/// Pure and deterministic.
/// </summary>
public interface IRegimeAnalyzer
{
    RegimeAnalysis Analyze(ReturnSeries returns, int window = 10);
}

public sealed class RegimeAnalyzer : IRegimeAnalyzer
{
    public RegimeAnalysis Analyze(ReturnSeries returns, int window = 10)
    {
        ArgumentNullException.ThrowIfNull(returns);
        var values = returns.Values;
        var n = values.Count;
        window = Math.Clamp(window, 2, Math.Max(2, n / 3));

        var rollingVol = new double[n];
        for (var i = 0; i < n; i++)
        {
            var start = Math.Max(0, i - window + 1);
            rollingVol[i] = StdDev(values, start, i + 1);
        }

        var sorted = (double[])rollingVol.Clone();
        Array.Sort(sorted);
        var low = Percentile(sorted, 1.0 / 3.0);
        var high = Percentile(sorted, 2.0 / 3.0);

        var regimeAt = new MarketRegime[n];
        for (var i = 0; i < n; i++)
            regimeAt[i] = rollingVol[i] <= low ? MarketRegime.Calm
                : rollingVol[i] >= high ? MarketRegime.Turbulent
                : MarketRegime.Normal;

        var labels = BuildLabels(regimeAt);
        var byRegime = Aggregate(values, regimeAt);
        var hurst = HurstExponent(values);

        return new RegimeAnalysis(labels, byRegime, hurst, BuildRationale(byRegime, hurst));
    }

    private static List<RegimeLabel> BuildLabels(MarketRegime[] regimeAt)
    {
        var labels = new List<RegimeLabel>();
        if (regimeAt.Length == 0) return labels;
        var start = 0;
        for (var i = 1; i < regimeAt.Length; i++)
        {
            if (regimeAt[i] == regimeAt[start]) continue;
            labels.Add(new RegimeLabel(start, i - 1, regimeAt[start]));
            start = i;
        }
        labels.Add(new RegimeLabel(start, regimeAt.Length - 1, regimeAt[start]));
        return labels;
    }

    private static List<RegimePerformance> Aggregate(IReadOnlyList<double> values, MarketRegime[] regimeAt)
    {
        var result = new List<RegimePerformance>();
        foreach (var regime in new[] { MarketRegime.Calm, MarketRegime.Normal, MarketRegime.Turbulent })
        {
            var bucket = new List<double>();
            for (var i = 0; i < values.Count; i++)
                if (regimeAt[i] == regime)
                    bucket.Add(values[i]);
            if (bucket.Count == 0) continue;

            var mean = 0.0;
            foreach (var v in bucket) mean += v;
            mean /= bucket.Count;
            var variance = 0.0;
            foreach (var v in bucket) variance += (v - mean) * (v - mean);
            variance /= bucket.Count;
            var std = Math.Sqrt(variance);
            result.Add(new RegimePerformance(regime, bucket.Count, mean, std, std > 0 ? mean / std : 0.0));
        }
        return result;
    }

    // Rescaled-range (R/S) analysis: regress log(R/S) on log(chunk size); slope estimates Hurst.
    private static double HurstExponent(IReadOnlyList<double> values)
    {
        var n = values.Count;
        if (n < 16) return 0.5;

        var logSizes = new List<double>();
        var logRs = new List<double>();
        for (var size = 8; size <= n / 2; size *= 2)
        {
            var chunks = n / size;
            var rsSum = 0.0;
            var rsCount = 0;
            for (var c = 0; c < chunks; c++)
            {
                var rs = RescaledRange(values, c * size, size);
                if (rs > 0.0) { rsSum += rs; rsCount++; }
            }
            if (rsCount == 0) continue;
            logSizes.Add(Math.Log(size));
            logRs.Add(Math.Log(rsSum / rsCount));
        }

        return logSizes.Count < 2 ? 0.5 : Slope(logSizes, logRs);
    }

    private static double RescaledRange(IReadOnlyList<double> values, int start, int size)
    {
        var mean = 0.0;
        for (var i = 0; i < size; i++) mean += values[start + i];
        mean /= size;

        double cumulative = 0, min = 0, max = 0, variance = 0;
        for (var i = 0; i < size; i++)
        {
            var d = values[start + i] - mean;
            cumulative += d;
            if (cumulative > max) max = cumulative;
            if (cumulative < min) min = cumulative;
            variance += d * d;
        }
        var std = Math.Sqrt(variance / size);
        return std > 0.0 ? (max - min) / std : 0.0;
    }

    private static double Slope(List<double> x, List<double> y)
    {
        var n = x.Count;
        double mx = 0, my = 0;
        for (var i = 0; i < n; i++) { mx += x[i]; my += y[i]; }
        mx /= n; my /= n;
        double cov = 0, varX = 0;
        for (var i = 0; i < n; i++)
        {
            cov += (x[i] - mx) * (y[i] - my);
            varX += (x[i] - mx) * (x[i] - mx);
        }
        return varX > 0.0 ? cov / varX : 0.5;
    }

    private static double StdDev(IReadOnlyList<double> values, int start, int end)
    {
        var count = end - start;
        if (count < 2) return 0.0;
        var mean = 0.0;
        for (var i = start; i < end; i++) mean += values[i];
        mean /= count;
        var variance = 0.0;
        for (var i = start; i < end; i++) variance += (values[i] - mean) * (values[i] - mean);
        return Math.Sqrt(variance / count);
    }

    private static double Percentile(double[] sortedAscending, double q)
    {
        if (sortedAscending.Length == 0) return 0.0;
        var idx = (int)Math.Clamp(q * (sortedAscending.Length - 1), 0, sortedAscending.Length - 1);
        return sortedAscending[idx];
    }

    private static string BuildRationale(List<RegimePerformance> byRegime, double hurst)
    {
        if (byRegime.Count == 0) return "No regime data.";
        var best = byRegime[0];
        var worst = byRegime[0];
        foreach (var p in byRegime)
        {
            if (p.Sharpe > best.Sharpe) best = p;
            if (p.Sharpe < worst.Sharpe) worst = p;
        }
        var trend = hurst > 0.55 ? "trending (persistent)" : hurst < 0.45 ? "mean-reverting" : "close to a random walk";
        return string.Format(
            CultureInfo.InvariantCulture,
            "Strongest in the {0} regime (Sharpe {1:0.00}) and weakest in the {2} regime (Sharpe {3:0.00}). Hurst exponent {4:0.00} — the series is {5}.",
            best.Regime, best.Sharpe, worst.Regime, worst.Sharpe, hurst, trend);
    }
}
