using System.Globalization;
using Core.Quant;

namespace Core.Portfolio;

/// <summary>
/// Volatility-target and fractional-Kelly position sizing for a single strategy. Recommends the smaller
/// (safer) of the two exposures, capped at the leverage limit. Pure and deterministic.
/// </summary>
public sealed record PositionSizing(
    double RealizedAnnualVolatility,
    double VolatilityTargetFraction,
    double FullKellyFraction,
    double FractionalKellyFraction,
    double RecommendedFraction,
    string Rationale);

public interface IPositionSizer
{
    PositionSizing Size(
        ReturnSeries returns,
        VolatilityTarget target,
        KellyFraction kelly,
        LeverageCap cap,
        double periodsPerYear = 252.0);
}

public sealed class PositionSizer : IPositionSizer
{
    public PositionSizing Size(
        ReturnSeries returns,
        VolatilityTarget target,
        KellyFraction kelly,
        LeverageCap cap,
        double periodsPerYear = 252.0)
    {
        ArgumentNullException.ThrowIfNull(returns);
        if (!(periodsPerYear > 0.0)) periodsPerYear = 252.0;

        var realizedAnnualVol = returns.StandardDeviation * Math.Sqrt(periodsPerYear);

        // Vol targeting: scale exposure so realized vol meets the target. A flat series has no risk, so
        // it maxes out at the cap rather than dividing by zero.
        var volFraction = realizedAnnualVol > 0.0
            ? Math.Min(target.Value / realizedAnnualVol, cap.Value)
            : cap.Value;

        // Continuous Kelly for a strategy with per-period mean μ and variance σ²: f* = μ / σ².
        var fullKelly = returns.Variance > 0.0 ? returns.Mean / returns.Variance : 0.0;
        var fractionalKelly = Clamp(fullKelly * kelly.Value, 0.0, cap.Value); // never recommend a short here

        var recommended = fullKelly <= 0.0
            ? 0.0 // no positive edge → do not size in
            : Clamp(Math.Min(volFraction, fractionalKelly), 0.0, cap.Value);

        return new PositionSizing(
            realizedAnnualVol,
            volFraction,
            fullKelly,
            fractionalKelly,
            recommended,
            BuildRationale(recommended, realizedAnnualVol, target, volFraction, fractionalKelly, fullKelly));
    }

    private static string BuildRationale(
        double recommended, double realizedAnnualVol, VolatilityTarget target,
        double volFraction, double fractionalKelly, double fullKelly)
    {
        if (fullKelly <= 0.0)
            return "No positive expected edge in this series (full Kelly ≤ 0) — the model recommends no exposure.";
        var driver = fractionalKelly <= volFraction ? "fractional-Kelly growth optimum" : "volatility target";
        return string.Format(
            CultureInfo.InvariantCulture,
            "Recommended exposure {0:0.00}× (the safer of a {1:0.00}× volatility-target sizing for {2:0.0}% target vs {3:0.00}× realized annual vol, and a {4:0.00}× fractional-Kelly sizing). Binding constraint: {5}.",
            recommended, volFraction, target.Percent, realizedAnnualVol * 100.0, fractionalKelly, driver);
    }

    private static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;
}
