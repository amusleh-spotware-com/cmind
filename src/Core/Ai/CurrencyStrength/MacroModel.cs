using Core.Constants;
using Core.Domain;

namespace Core.Ai.CurrencyStrength;

/// <summary>The macro drivers aggregated into a currency's strength. Majors + shared drivers plus the
/// EM/exotic-relevant extras; a driver irrelevant to a tier simply carries zero weight for that tier.</summary>
public enum MacroDriver
{
    // Current (calendar-fed where possible)
    PolicyRate,
    Inflation,
    GdpGrowth,
    Employment,
    TradeBalance,
    PolicyStance,
    SurpriseMomentum,

    // EM/exotic-relevant current
    RealYield,
    ExternalVulnerability,
    PoliticalInstitutionalRisk,
    TermsOfTrade,

    // Forward (AI-gathered trajectory)
    RateTrajectory,
    InflationTrend,
    GrowthMomentum,
    GeopoliticalRisk
}

/// <summary>Where a data point came from — the calendar is preferred; AI fills only what the calendar lacks.</summary>
public enum Provenance
{
    Calendar,
    Ai,
    Derived
}

/// <summary>Per-tier confidence in the sourced figures — surfaced as a reliability badge in the UI.</summary>
public enum DataConfidence
{
    High,
    Medium,
    Low
}

/// <summary>A central bank's policy lean; hawkish ⇒ stronger currency.</summary>
public enum PolicyStance
{
    Dovish = -1,
    Neutral = 0,
    Hawkish = 1
}

/// <summary>
/// Raw per-currency macro inputs for the current ranking. Majors bind the core fields from the calendar's
/// latest release; EM/exotics add carry/vulnerability/politics/terms-of-trade fields (nullable — absent ⇒
/// zero weight, not a bad guess). Immutable; range-guarded (exotic edge cases such as hyperinflation or thin
/// reserves are accepted-but-flagged via a low <see cref="Confidence"/>, never rejected).
/// </summary>
public sealed record CurrencyIndicators
{
    public required Currency Currency { get; init; }

    /// <summary>Policy rate, percent (e.g. 5.25).</summary>
    public required double PolicyRate { get; init; }

    /// <summary>Headline CPI, percent YoY.</summary>
    public required double Cpi { get; init; }

    /// <summary>The central bank's inflation target, percent (typically 2.0).</summary>
    public double InflationTarget { get; init; } = 2.0;

    /// <summary>Real GDP growth, percent YoY.</summary>
    public required double GdpGrowth { get; init; }

    /// <summary>Unemployment rate, percent (lower ⇒ stronger).</summary>
    public required double Unemployment { get; init; }

    /// <summary>Trade balance / current account as percent of GDP (surplus ⇒ stronger).</summary>
    public double TradeBalancePercentGdp { get; init; }

    public PolicyStance Stance { get; init; } = PolicyStance.Neutral;

    /// <summary>Sum of recent release surprise z-scores (beats add, misses subtract) — momentum from the calendar.</summary>
    public double SurpriseMomentum { get; init; }

    // EM/exotic-relevant (nullable — driver gets zero weight when absent).
    public double? RealYield { get; init; }

    /// <summary>External-vulnerability score, higher ⇒ worse (CA/fiscal deficit, low reserves, USD debt).</summary>
    public double? ExternalVulnerability { get; init; }

    /// <summary>Political/institutional-risk score, higher ⇒ worse (elections, CB independence, capital controls).</summary>
    public double? PoliticalRisk { get; init; }

    /// <summary>Terms-of-trade signal for commodity exporters, positive ⇒ improving.</summary>
    public double? TermsOfTrade { get; init; }

    public Provenance Provenance { get; init; } = Provenance.Ai;
    public DataConfidence Confidence { get; init; } = DataConfidence.High;

    /// <summary>The point-in-time instant this snapshot's calendar data was known at (no look-ahead).</summary>
    public DateTimeOffset? KnownAt { get; init; }

    /// <summary>Validates ranges appropriate to the currency's tier; exotic extremes are allowed but the caller
    /// is expected to carry a low <see cref="Confidence"/>. Guards only against non-finite / absurd values.</summary>
    public CurrencyIndicators Validate()
    {
        Guard(PolicyRate, -5, 200);
        Guard(Cpi, -30, 1_000_000);
        Guard(InflationTarget, 0, 50);
        Guard(GdpGrowth, -50, 100);
        Guard(Unemployment, 0, 100);
        Guard(TradeBalancePercentGdp, -100, 100);
        if (RealYield is { } ry) Guard(ry, -1_000_000, 1_000_000);
        if (ExternalVulnerability is { } ev) Guard(ev, -1000, 1000);
        if (PoliticalRisk is { } pr) Guard(pr, -1000, 1000);
        if (TermsOfTrade is { } tt) Guard(tt, -1000, 1000);
        return this;
    }

    private static void Guard(double value, double min, double max)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < min || value > max)
            throw new DomainException(DomainErrors.CurrencyIndicatorOutOfRange);
    }
}

/// <summary>
/// Per-currency forward inputs — the expected trajectory over a horizon. Gathered by AI (the calendar has no
/// forward view) and seeded by calendar surprise momentum where available. Range-guarded, immutable.
/// </summary>
public sealed record CurrencyTrajectory
{
    public required Currency Currency { get; init; }

    /// <summary>Expected change in the policy rate over the horizon, basis points (hiking +, cutting −).</summary>
    public required double ExpectedRatePathBp { get; init; }

    /// <summary>Inflation trend vs target: negative ⇒ moving toward target (supportive), positive ⇒ away.</summary>
    public double InflationTrend { get; init; }

    /// <summary>Growth momentum: positive ⇒ accelerating relative growth.</summary>
    public double GrowthMomentum { get; init; }

    /// <summary>Forward geopolitical/risk delta for this currency: positive ⇒ tailwind (safe-haven bid in
    /// risk-off for USD/JPY/CHF), negative ⇒ headwind (tariffs, fiscal/debt, election risk).</summary>
    public double GeopoliticalDelta { get; init; }

    public Provenance Provenance { get; init; } = Provenance.Ai;
    public DataConfidence Confidence { get; init; } = DataConfidence.Medium;

    public CurrencyTrajectory Validate()
    {
        Guard(ExpectedRatePathBp, -5000, 5000);
        Guard(InflationTrend, -100, 100);
        Guard(GrowthMomentum, -100, 100);
        Guard(GeopoliticalDelta, -10, 10);
        return this;
    }

    private static void Guard(double value, double min, double max)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < min || value > max)
            throw new DomainException(DomainErrors.CurrencyTrajectoryOutOfRange);
    }
}

/// <summary>The per-driver breakdown behind a score — the "why" the charts render.</summary>
public sealed record DriverScore(
    MacroDriver Driver,
    double Normalized,
    double Weight,
    double Contribution,
    string Rationale);
