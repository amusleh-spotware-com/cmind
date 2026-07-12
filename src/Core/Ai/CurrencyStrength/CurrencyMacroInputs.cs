namespace Core.Ai.CurrencyStrength;

/// <summary>
/// A partial set of macro figures for one currency, from a single source (calendar or AI). Every field is
/// nullable so a source contributes only what it actually has; the deterministic <see cref="CurrencyMacroBinder"/>
/// merges a calendar partial (preferred) with an AI partial (gap-fill) into a complete <see cref="CurrencyIndicators"/>.
/// </summary>
public sealed record CurrencyMacroInputs
{
    public double? PolicyRate { get; init; }
    public double? Cpi { get; init; }
    public double? InflationTarget { get; init; }
    public double? GdpGrowth { get; init; }
    public double? Unemployment { get; init; }
    public double? TradeBalancePercentGdp { get; init; }
    public PolicyStance? Stance { get; init; }
    public double? SurpriseMomentum { get; init; }
    public double? RealYield { get; init; }
    public double? ExternalVulnerability { get; init; }
    public double? PoliticalRisk { get; init; }
    public double? TermsOfTrade { get; init; }
    public DataConfidence? Confidence { get; init; }
    public DateTimeOffset? KnownAt { get; init; }

    /// <summary>True when this partial carries at least one core current figure (rate/CPI/GDP/unemployment).</summary>
    public bool HasCoreFigure =>
        PolicyRate is not null || Cpi is not null || GdpGrowth is not null || Unemployment is not null;
}

/// <summary>
/// Pure, deterministic merge of a calendar-sourced partial (preferred, point-in-time) with an AI-gathered
/// partial (gap-fill) into a full <see cref="CurrencyIndicators"/>. Per field the calendar value wins; where
/// neither source has a figure a neutral default is used so the engine never sees a missing required input.
/// Provenance is <c>Calendar</c> when the calendar contributed any core figure, otherwise <c>Ai</c>.
/// </summary>
public static class CurrencyMacroBinder
{
    public static CurrencyIndicators Merge(Currency currency, CurrencyMacroInputs? calendar, CurrencyMacroInputs? ai)
    {
        var target = Pick(calendar?.InflationTarget, ai?.InflationTarget, 2.0);
        var provenance = calendar is { HasCoreFigure: true } ? Provenance.Calendar : Provenance.Ai;
        var confidence = Worst(calendar?.Confidence, ai?.Confidence, provenance == Provenance.Calendar ? DataConfidence.High : DataConfidence.Medium);

        return new CurrencyIndicators
        {
            Currency = currency,
            PolicyRate = Pick(calendar?.PolicyRate, ai?.PolicyRate, 0.0),
            Cpi = Pick(calendar?.Cpi, ai?.Cpi, target),
            InflationTarget = target,
            GdpGrowth = Pick(calendar?.GdpGrowth, ai?.GdpGrowth, 0.0),
            Unemployment = Pick(calendar?.Unemployment, ai?.Unemployment, 5.0),
            TradeBalancePercentGdp = Pick(calendar?.TradeBalancePercentGdp, ai?.TradeBalancePercentGdp, 0.0),
            Stance = calendar?.Stance ?? ai?.Stance ?? PolicyStance.Neutral,
            SurpriseMomentum = Pick(calendar?.SurpriseMomentum, ai?.SurpriseMomentum, 0.0),
            RealYield = calendar?.RealYield ?? ai?.RealYield,
            ExternalVulnerability = calendar?.ExternalVulnerability ?? ai?.ExternalVulnerability,
            PoliticalRisk = calendar?.PoliticalRisk ?? ai?.PoliticalRisk,
            TermsOfTrade = calendar?.TermsOfTrade ?? ai?.TermsOfTrade,
            Provenance = provenance,
            Confidence = confidence,
            KnownAt = calendar?.KnownAt ?? ai?.KnownAt
        }.Validate();
    }

    private static double Pick(double? preferred, double? fallback, double @default) =>
        preferred ?? fallback ?? @default;

    private static DataConfidence Worst(DataConfidence? a, DataConfidence? b, DataConfidence @default)
    {
        var worst = @default;
        if (a is { } av && (int)av > (int)worst) worst = av;
        if (b is { } bv && (int)bv > (int)worst) worst = bv;
        return worst;
    }
}
