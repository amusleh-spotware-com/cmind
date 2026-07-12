using Core.Ai.CurrencyStrength;

namespace UnitTests.Ai.CurrencyStrength;

/// <summary>Deterministic builders for the currency-strength engine tests — no clock, no randomness.</summary>
internal static class CurrencyTestData
{
    public static readonly DateTimeOffset AsOf = new(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);

    public static Currency Major(string code) => new(code, CurrencyTier.Major);
    public static Currency Em(string code) => new(code, CurrencyTier.EmergingMarket);
    public static Currency Exotic(string code) => new(code, CurrencyTier.Exotic);
    public static Currency Pegged(string code, string anchor) => new(code, CurrencyTier.Exotic, isPegged: true, pegAnchor: anchor);

    public static CurrencyIndicators Indicators(
        Currency currency,
        double policyRate = 2.0,
        double cpi = 2.0,
        double gdp = 2.0,
        double unemployment = 4.0,
        double tradeBalance = 0.0,
        PolicyStance stance = PolicyStance.Neutral,
        double surprise = 0.0,
        double? realYield = null,
        double? externalVulnerability = null,
        double? politicalRisk = null,
        double? termsOfTrade = null,
        DataConfidence confidence = DataConfidence.High) =>
        new CurrencyIndicators
        {
            Currency = currency,
            PolicyRate = policyRate,
            Cpi = cpi,
            GdpGrowth = gdp,
            Unemployment = unemployment,
            TradeBalancePercentGdp = tradeBalance,
            Stance = stance,
            SurpriseMomentum = surprise,
            RealYield = realYield,
            ExternalVulnerability = externalVulnerability,
            PoliticalRisk = politicalRisk,
            TermsOfTrade = termsOfTrade,
            Confidence = confidence
        }.Validate();

    public static CurrencyTrajectory Trajectory(
        Currency currency,
        double ratePathBp = 0.0,
        double inflationTrend = 0.0,
        double growthMomentum = 0.0,
        double geopoliticalDelta = 0.0) =>
        new CurrencyTrajectory
        {
            Currency = currency,
            ExpectedRatePathBp = ratePathBp,
            InflationTrend = inflationTrend,
            GrowthMomentum = growthMomentum,
            GeopoliticalDelta = geopoliticalDelta
        }.Validate();
}
