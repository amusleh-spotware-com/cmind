namespace Core.Ai.CurrencyStrength;

/// <summary>How a snapshot's figures were sourced — drives the reliability badge in the UI.</summary>
public enum SnapshotSource
{
    CalendarAndAi,
    CalendarOnly,
    AiOnly
}

/// <summary>One per-driver contribution row (current or forward), as served to consumers.</summary>
public sealed record DriverRow(string Driver, double Normalized, double Weight, double Contribution, string Rationale);

/// <summary>One currency's current ranking row.</summary>
public sealed record RankRow(string Code, string Tier, int Rank, double Composite, string Confidence, IReadOnlyList<DriverRow> Drivers);

/// <summary>One currency's forward forecast at the served horizon.</summary>
public sealed record ForecastRow(string Code, string Tier, double Projected, double Current, string Confidence);

/// <summary>One directional cross's forward outlook at the served horizon.</summary>
public sealed record PairRow(
    string Base, string Quote, string Bias, double Conviction, double Differential,
    string Confidence, bool Pegged, IReadOnlyList<DriverRow> Why);

/// <summary>The read model served for one horizon — ranking + forward forecasts + pair-outlook matrix + narrative.</summary>
public sealed record CurrencyStrengthView(
    DateTimeOffset AsOf,
    string Horizon,
    string Source,
    DateTimeOffset? CalendarKnownAt,
    string Narrative,
    IReadOnlyList<RankRow> Ranking,
    IReadOnlyList<ForecastRow> Forecasts,
    IReadOnlyList<PairRow> Pairs);

/// <summary>One horizon's forward layer inside the stored snapshot payload.</summary>
public sealed record HorizonLayer(IReadOnlyList<ForecastRow> Forecasts, IReadOnlyList<PairRow> Pairs);

/// <summary>The full multi-horizon payload serialized into a <c>CurrencyStrengthSnapshot</c>.</summary>
public sealed record CurrencyStrengthSnapshotData(
    IReadOnlyList<RankRow> Ranking,
    IReadOnlyDictionary<string, HorizonLayer> Horizons);

/// <summary>One point in a historical strength time series (composite per currency at a snapshot instant).</summary>
public sealed record StrengthHistoryPoint(DateTimeOffset AsOf, IReadOnlyDictionary<string, double> Composite);

/// <summary>Pure Core mapping from the deterministic engine VOs to the serializable read-model rows.</summary>
public static class CurrencyStrengthMapper
{
    public static IReadOnlyList<RankRow> ToRankRows(CurrencyStrengthRanking ranking)
    {
        var rows = new List<RankRow>(ranking.Scores.Count);
        foreach (var score in ranking.Scores)
            rows.Add(new RankRow(
                score.Currency.Code, score.Currency.Tier.ToString(), ranking.Rank(score.Currency),
                score.Composite, score.Confidence.ToString(), ToDriverRows(score.Breakdown)));
        return rows;
    }

    public static HorizonLayer ToLayer(IReadOnlyList<CurrencyForecast> forecasts, PairOutlookMatrix matrix)
    {
        var forecastRows = forecasts
            .Select(f => new ForecastRow(f.Currency.Code, f.Currency.Tier.ToString(), f.ProjectedScore, f.CurrentScore, f.Confidence.ToString()))
            .ToList();
        var pairRows = matrix.Pairs
            .Select(p => new PairRow(
                p.Base.Code, p.Quote.Code, p.Bias.ToString(), p.Conviction, p.ProjectedDifferential,
                p.Confidence.ToString(), p.Pegged, ToDriverRows(p.WhyBreakdown)))
            .ToList();
        return new HorizonLayer(forecastRows, pairRows);
    }

    private static IReadOnlyList<DriverRow> ToDriverRows(IReadOnlyList<DriverScore> breakdown) =>
        [.. breakdown.Select(d => new DriverRow(d.Driver.ToString(), d.Normalized, d.Weight, d.Contribution, d.Rationale))];
}
