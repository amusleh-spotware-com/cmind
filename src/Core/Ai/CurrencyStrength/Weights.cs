using Core.Constants;
using Core.Domain;

namespace Core.Ai.CurrencyStrength;

/// <summary>
/// Tier-keyed weights for the <em>current</em> ranking drivers. Each tier's non-negative weights must sum to
/// 1 (validated at construction). Majors weight rate level + trajectory highest; EM/exotics weight carry,
/// external vulnerability and political risk up. Overridable per deployment without touching the engine.
/// </summary>
public sealed class StrengthWeights
{
    private readonly IReadOnlyDictionary<CurrencyTier, IReadOnlyDictionary<MacroDriver, double>> _byTier;

    private StrengthWeights(IReadOnlyDictionary<CurrencyTier, IReadOnlyDictionary<MacroDriver, double>> byTier) =>
        _byTier = byTier;

    public static StrengthWeights Create(
        IReadOnlyDictionary<CurrencyTier, IReadOnlyDictionary<MacroDriver, double>> byTier)
    {
        ArgumentNullException.ThrowIfNull(byTier);
        foreach (var (_, weights) in byTier)
            WeightValidation.EnsureNormalized(weights.Values);
        return new StrengthWeights(byTier);
    }

    public IReadOnlyDictionary<MacroDriver, double> ForTier(CurrencyTier tier) =>
        _byTier.TryGetValue(tier, out var w) ? w : _byTier[CurrencyTier.Major];

    /// <summary>Institution-grade defaults: majors rate-led; EM/exotics carry/risk/vulnerability-led.</summary>
    public static StrengthWeights Default() => Create(new Dictionary<CurrencyTier, IReadOnlyDictionary<MacroDriver, double>>
    {
        [CurrencyTier.Major] = new Dictionary<MacroDriver, double>
        {
            [MacroDriver.PolicyRate] = 0.30,
            [MacroDriver.PolicyStance] = 0.15,
            [MacroDriver.Inflation] = 0.15,
            [MacroDriver.GdpGrowth] = 0.13,
            [MacroDriver.Employment] = 0.10,
            [MacroDriver.TradeBalance] = 0.07,
            [MacroDriver.SurpriseMomentum] = 0.10
        },
        [CurrencyTier.EmergingMarket] = new Dictionary<MacroDriver, double>
        {
            [MacroDriver.PolicyRate] = 0.16,
            [MacroDriver.RealYield] = 0.20,
            [MacroDriver.ExternalVulnerability] = 0.16,
            [MacroDriver.PolicyStance] = 0.08,
            [MacroDriver.Inflation] = 0.10,
            [MacroDriver.GdpGrowth] = 0.08,
            [MacroDriver.TradeBalance] = 0.06,
            [MacroDriver.TermsOfTrade] = 0.06,
            [MacroDriver.PoliticalInstitutionalRisk] = 0.05,
            [MacroDriver.SurpriseMomentum] = 0.05
        },
        [CurrencyTier.Exotic] = new Dictionary<MacroDriver, double>
        {
            [MacroDriver.RealYield] = 0.22,
            [MacroDriver.ExternalVulnerability] = 0.20,
            [MacroDriver.PoliticalInstitutionalRisk] = 0.14,
            [MacroDriver.PolicyRate] = 0.12,
            [MacroDriver.Inflation] = 0.10,
            [MacroDriver.TermsOfTrade] = 0.08,
            [MacroDriver.GdpGrowth] = 0.06,
            [MacroDriver.TradeBalance] = 0.04,
            [MacroDriver.SurpriseMomentum] = 0.04
        }
    });
}

/// <summary>
/// Tier-keyed weights for the <em>forward</em> trajectory drivers. Rate path dominates; geopolitics is
/// bounded but carries more weight for EM/exotics (high-beta to global risk). Each tier sums to 1.
/// </summary>
public sealed class ForwardWeights
{
    private readonly IReadOnlyDictionary<CurrencyTier, IReadOnlyDictionary<MacroDriver, double>> _byTier;

    private ForwardWeights(IReadOnlyDictionary<CurrencyTier, IReadOnlyDictionary<MacroDriver, double>> byTier) =>
        _byTier = byTier;

    public static ForwardWeights Create(
        IReadOnlyDictionary<CurrencyTier, IReadOnlyDictionary<MacroDriver, double>> byTier)
    {
        ArgumentNullException.ThrowIfNull(byTier);
        foreach (var (_, weights) in byTier)
            WeightValidation.EnsureNormalized(weights.Values);
        return new ForwardWeights(byTier);
    }

    public IReadOnlyDictionary<MacroDriver, double> ForTier(CurrencyTier tier) =>
        _byTier.TryGetValue(tier, out var w) ? w : _byTier[CurrencyTier.Major];

    public static ForwardWeights Default() => Create(new Dictionary<CurrencyTier, IReadOnlyDictionary<MacroDriver, double>>
    {
        [CurrencyTier.Major] = new Dictionary<MacroDriver, double>
        {
            [MacroDriver.RateTrajectory] = 0.45,
            [MacroDriver.InflationTrend] = 0.20,
            [MacroDriver.GrowthMomentum] = 0.20,
            [MacroDriver.GeopoliticalRisk] = 0.15
        },
        [CurrencyTier.EmergingMarket] = new Dictionary<MacroDriver, double>
        {
            [MacroDriver.RateTrajectory] = 0.35,
            [MacroDriver.InflationTrend] = 0.18,
            [MacroDriver.GrowthMomentum] = 0.17,
            [MacroDriver.GeopoliticalRisk] = 0.30
        },
        [CurrencyTier.Exotic] = new Dictionary<MacroDriver, double>
        {
            [MacroDriver.RateTrajectory] = 0.30,
            [MacroDriver.InflationTrend] = 0.15,
            [MacroDriver.GrowthMomentum] = 0.15,
            [MacroDriver.GeopoliticalRisk] = 0.40
        }
    });
}

internal static class WeightValidation
{
    public static void EnsureNormalized(IEnumerable<double> weights)
    {
        var sum = 0.0;
        foreach (var w in weights)
        {
            if (double.IsNaN(w) || w < 0)
                throw new DomainException(DomainErrors.CurrencyWeightsNotNormalized);
            sum += w;
        }

        if (Math.Abs(sum - 1.0) > 1e-6)
            throw new DomainException(DomainErrors.CurrencyWeightsNotNormalized);
    }
}
