using Core.Constants;
using Core.Domain;

namespace Core.Ai.CurrencyStrength;

/// <summary>
/// The deterministic current-ranking engine. Scores each driver as a within-tier z-score (so a 50%-inflation
/// exotic never distorts the majors' distribution), winsorizes outliers, weight-sums into one composite per
/// currency, and ranks strongest→weakest with a stable ISO-code tie-break. Pure — no I/O, no clock, no LLM.
/// </summary>
public static class CurrencyStrengthCalculator
{
    internal const double WinsorLimit = 3.0;

    public static CurrencyStrengthRanking Compute(
        IReadOnlyList<CurrencyIndicators> panel, StrengthWeights weights, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(weights);
        if (panel.Count == 0) throw new DomainException(DomainErrors.CurrencyPanelEmpty);

        // Per (tier, driver) z-scores, computed within the tier only.
        var zByCode = new Dictionary<string, Dictionary<MacroDriver, double>>(StringComparer.Ordinal);
        foreach (var indicators in panel)
            zByCode[indicators.Currency.Code] = [];

        foreach (var tierGroup in panel.GroupBy(p => p.Currency.Tier))
        {
            var members = tierGroup.ToList();
            var tierWeights = weights.ForTier(tierGroup.Key);
            foreach (var driver in tierWeights.Keys)
            {
                var raws = members
                    .Select(m => (m.Currency.Code, Raw: CurrentRaw(driver, m)))
                    .Where(x => x.Raw is not null)
                    .ToList();
                var zs = Normalization.ZScores(raws.Select(x => x.Raw!.Value).ToList(), WinsorLimit);
                for (var i = 0; i < raws.Count; i++)
                    zByCode[raws[i].Code][driver] = zs[i];
            }
        }

        var scores = new List<CurrencyStrengthScore>(panel.Count);
        foreach (var indicators in panel)
        {
            var tierWeights = weights.ForTier(indicators.Currency.Tier);
            var breakdown = new List<DriverScore>(tierWeights.Count);
            var composite = 0.0;
            foreach (var (driver, weight) in tierWeights)
            {
                var z = zByCode[indicators.Currency.Code].GetValueOrDefault(driver, 0.0);
                var contribution = z * weight;
                composite += contribution;
                breakdown.Add(new DriverScore(driver, z, weight, contribution, Rationale(driver, z)));
            }

            scores.Add(new CurrencyStrengthScore(indicators.Currency, composite, breakdown, indicators.Confidence));
        }

        scores.Sort((a, b) =>
        {
            var byComposite = b.Composite.CompareTo(a.Composite);
            return byComposite != 0 ? byComposite : string.CompareOrdinal(a.Currency.Code, b.Currency.Code);
        });

        return new CurrencyStrengthRanking(scores, asOf);
    }

    /// <summary>The signed raw driver value; higher ⇒ stronger currency. Null ⇒ driver absent for this currency
    /// (excluded from the tier's stats and contributes nothing).</summary>
    internal static double? CurrentRaw(MacroDriver driver, CurrencyIndicators i) => driver switch
    {
        MacroDriver.PolicyRate => i.PolicyRate,
        MacroDriver.Inflation => -(i.Cpi - i.InflationTarget),
        MacroDriver.GdpGrowth => i.GdpGrowth,
        MacroDriver.Employment => -i.Unemployment,
        MacroDriver.TradeBalance => i.TradeBalancePercentGdp,
        MacroDriver.PolicyStance => (double)(int)i.Stance,
        MacroDriver.SurpriseMomentum => i.SurpriseMomentum,
        MacroDriver.RealYield => i.RealYield ?? (i.PolicyRate - i.Cpi),
        MacroDriver.ExternalVulnerability => i.ExternalVulnerability is { } ev ? -ev : null,
        MacroDriver.PoliticalInstitutionalRisk => i.PoliticalRisk is { } pr ? -pr : null,
        MacroDriver.TermsOfTrade => i.TermsOfTrade,
        _ => null
    };

    private static string Rationale(MacroDriver driver, double z)
    {
        var lean = z > 0.2 ? "supportive" : z < -0.2 ? "a drag" : "neutral";
        return $"{driver} is {lean} ({z:+0.00;-0.00;0.00}σ vs tier).";
    }
}

/// <summary>Deterministic standardization: winsorized z-scores over a panel. A degenerate panel (n&lt;2 or
/// zero spread) yields all-zero z-scores — no NaN, no divide-by-zero.</summary>
internal static class Normalization
{
    public static IReadOnlyList<double> ZScores(IReadOnlyList<double> values, double winsorLimit)
    {
        var n = values.Count;
        if (n < 2) return [.. Enumerable.Repeat(0.0, n)];

        var mean = values.Average();
        var variance = values.Sum(v => (v - mean) * (v - mean)) / n;
        var stdDev = Math.Sqrt(variance);
        if (stdDev <= 1e-12) return [.. Enumerable.Repeat(0.0, n)];

        var result = new double[n];
        for (var i = 0; i < n; i++)
        {
            var z = (values[i] - mean) / stdDev;
            result[i] = Math.Clamp(z, -winsorLimit, winsorLimit);
        }

        return result;
    }
}
