using System.Globalization;
using Core.Constants;
using Core.Domain;
using Core.Quant;

namespace Core.Portfolio;

/// <summary>
/// Allocates capital across several strategies by inverse-volatility risk parity, reports the resulting
/// correlation matrix and projected portfolio volatility, then scales the book to a target volatility
/// (capped). Deterministic; the covariance is the sample estimate over the aligned return series.
/// </summary>
public sealed record PortfolioAllocationResult(
    IReadOnlyList<double> Weights,
    IReadOnlyList<double> ScaledWeights,
    double ProjectedAnnualVolatility,
    double Leverage,
    double[][] Correlation,
    string Rationale);

public interface IPortfolioAllocator
{
    PortfolioAllocationResult Allocate(
        IReadOnlyList<ReturnSeries> strategies,
        VolatilityTarget target,
        LeverageCap cap,
        double periodsPerYear = 252.0);
}

public sealed class PortfolioAllocator : IPortfolioAllocator
{
    private const double MinVolatility = 1e-9;

    public PortfolioAllocationResult Allocate(
        IReadOnlyList<ReturnSeries> strategies,
        VolatilityTarget target,
        LeverageCap cap,
        double periodsPerYear = 252.0)
    {
        ArgumentNullException.ThrowIfNull(strategies);
        if (strategies.Count < 2) throw new DomainException(DomainErrors.PortfolioInsufficientStrategies);
        var length = strategies[0].Count;
        if (strategies.Any(s => s.Count != length)) throw new DomainException(DomainErrors.PortfolioSeriesMismatch);
        if (!(periodsPerYear > 0.0)) periodsPerYear = 252.0;

        var n = strategies.Count;
        var vol = new double[n];
        for (var i = 0; i < n; i++)
        {
            // A zero-volatility (flat) leg is riskless: inverse-vol risk parity is undefined for it (it
            // would swallow the entire book) and its correlation is undefined. Reject rather than misallocate.
            if (!(strategies[i].StandardDeviation > MinVolatility))
                throw new DomainException(DomainErrors.PortfolioDegenerateStrategy);
            vol[i] = strategies[i].StandardDeviation;
        }

        // Inverse-volatility (naive risk parity) weights, normalized to sum 1.
        var weights = new double[n];
        var inverseSum = 0.0;
        for (var i = 0; i < n; i++) inverseSum += 1.0 / vol[i];
        for (var i = 0; i < n; i++) weights[i] = 1.0 / vol[i] / inverseSum;

        var covariance = Covariance(strategies, length);
        var correlation = Correlation(covariance, vol, n);

        var portfolioVariance = 0.0;
        for (var i = 0; i < n; i++)
            for (var j = 0; j < n; j++)
                portfolioVariance += weights[i] * weights[j] * covariance[i][j];
        var projectedAnnualVol = Math.Sqrt(Math.Max(0.0, portfolioVariance)) * Math.Sqrt(periodsPerYear);

        var leverage = projectedAnnualVol > 0.0
            ? Math.Min(target.Value / projectedAnnualVol, cap.Value)
            : cap.Value;

        var scaled = new double[n];
        for (var i = 0; i < n; i++) scaled[i] = weights[i] * leverage;

        return new PortfolioAllocationResult(
            weights, scaled, projectedAnnualVol, leverage, correlation,
            string.Format(
                CultureInfo.InvariantCulture,
                "Inverse-volatility risk parity across {0} strategies. Projected portfolio volatility {1:0.0}% per year; scaled {2:0.00}× to meet the {3:0.0}% target (leverage capped at {4:0.0}×).",
                n, projectedAnnualVol * 100.0, leverage, target.Percent, cap.Value));
    }

    private static double[][] Covariance(IReadOnlyList<ReturnSeries> strategies, int length)
    {
        var n = strategies.Count;
        var cov = new double[n][];
        for (var i = 0; i < n; i++)
        {
            cov[i] = new double[n];
            for (var j = 0; j < n; j++)
            {
                var xi = strategies[i].Values;
                var xj = strategies[j].Values;
                var mi = strategies[i].Mean;
                var mj = strategies[j].Mean;
                var sum = 0.0;
                for (var k = 0; k < length; k++) sum += (xi[k] - mi) * (xj[k] - mj);
                cov[i][j] = sum / length;
            }
        }
        return cov;
    }

    private static double[][] Correlation(double[][] cov, double[] vol, int n)
    {
        var corr = new double[n][];
        for (var i = 0; i < n; i++)
        {
            corr[i] = new double[n];
            for (var j = 0; j < n; j++)
                corr[i][j] = cov[i][j] / (vol[i] * vol[j]);
        }
        return corr;
    }
}
