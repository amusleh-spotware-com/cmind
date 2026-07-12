namespace Core.Quant;

/// <summary>
/// Deterministic, dependency-free numerical helpers for the quant statistics. The standard-normal CDF
/// uses Abramowitz-Stegun 7.1.26 (|error| &lt; 1.5e-7); the inverse CDF uses Acklam's rational
/// approximation (|error| &lt; 1.15e-9). Both are pure functions so every quant result is reproducible.
/// </summary>
public static class QuantMath
{
    /// <summary>Euler-Mascheroni constant, used by the False Strategy Theorem (expected maximum Sharpe).</summary>
    public const double EulerMascheroni = 0.5772156649015329;

    /// <summary>Standard-normal cumulative distribution function Φ(x).</summary>
    public static double NormalCdf(double x)
    {
        // Φ(x) = 0.5 · erfc(-x / √2)
        return 0.5 * Erfc(-x / Math.Sqrt(2.0));
    }

    /// <summary>Inverse standard-normal CDF (probit). Domain (0,1); clamps just inside the open interval.</summary>
    public static double NormalInverseCdf(double p)
    {
        if (double.IsNaN(p)) return double.NaN;
        if (p <= 0.0) return double.NegativeInfinity;
        if (p >= 1.0) return double.PositiveInfinity;

        // Acklam's algorithm.
        const double a1 = -3.969683028665376e+01, a2 = 2.209460984245205e+02, a3 = -2.759285104469687e+02,
            a4 = 1.383577518672690e+02, a5 = -3.066479806614716e+01, a6 = 2.506628277459239e+00;
        const double b1 = -5.447609879822406e+01, b2 = 1.615858368580409e+02, b3 = -1.556989798598866e+02,
            b4 = 6.680131188771972e+01, b5 = -1.328068155288572e+01;
        const double c1 = -7.784894002430293e-03, c2 = -3.223964580411365e-01, c3 = -2.400758277161838e+00,
            c4 = -2.549732539343734e+00, c5 = 4.374664141464968e+00, c6 = 2.938163982698783e+00;
        const double d1 = 7.784695709041462e-03, d2 = 3.224671290700398e-01, d3 = 2.445134137142996e+00,
            d4 = 3.754408661907416e+00;
        const double pLow = 0.02425, pHigh = 1.0 - pLow;

        double q, r;
        if (p < pLow)
        {
            q = Math.Sqrt(-2.0 * Math.Log(p));
            return (((((c1 * q + c2) * q + c3) * q + c4) * q + c5) * q + c6) /
                   ((((d1 * q + d2) * q + d3) * q + d4) * q + 1.0);
        }
        if (p <= pHigh)
        {
            q = p - 0.5;
            r = q * q;
            return (((((a1 * r + a2) * r + a3) * r + a4) * r + a5) * r + a6) * q /
                   (((((b1 * r + b2) * r + b3) * r + b4) * r + b5) * r + 1.0);
        }
        q = Math.Sqrt(-2.0 * Math.Log(1.0 - p));
        return -(((((c1 * q + c2) * q + c3) * q + c4) * q + c5) * q + c6) /
               ((((d1 * q + d2) * q + d3) * q + d4) * q + 1.0);
    }

    /// <summary>Complementary error function erfc(x) via Abramowitz-Stegun 7.1.26.</summary>
    private static double Erfc(double x)
    {
        var z = Math.Abs(x);
        var t = 1.0 / (1.0 + 0.5 * z);
        var ans = t * Math.Exp(-z * z - 1.26551223 + t * (1.00002368 + t * (0.37409196 + t * (0.09678418 +
            t * (-0.18628806 + t * (0.27886807 + t * (-1.13520398 + t * (1.48851587 +
            t * (-0.82215223 + t * 0.17087277)))))))));
        return x >= 0.0 ? ans : 2.0 - ans;
    }
}
