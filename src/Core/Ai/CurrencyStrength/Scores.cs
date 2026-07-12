using Core.Constants;
using Core.Domain;

namespace Core.Ai.CurrencyStrength;

/// <summary>Forward projection horizons. Longer horizon ⇒ trajectory terms weigh more vs the current level.</summary>
public enum Horizon
{
    OneMonth,
    ThreeMonths,
    SixMonths,
    TwelveMonths
}

public static class HorizonExtensions
{
    /// <summary>How strongly the trajectory terms weigh vs the current level at this horizon.</summary>
    public static double Scale(this Horizon horizon) => horizon switch
    {
        Horizon.OneMonth => 0.25,
        Horizon.ThreeMonths => 0.50,
        Horizon.SixMonths => 0.75,
        Horizon.TwelveMonths => 1.00,
        _ => throw new DomainException(DomainErrors.CurrencyHorizonUnknown)
    };

    public static string Label(this Horizon horizon) => horizon switch
    {
        Horizon.OneMonth => "1M",
        Horizon.ThreeMonths => "3M",
        Horizon.SixMonths => "6M",
        Horizon.TwelveMonths => "12M",
        _ => throw new DomainException(DomainErrors.CurrencyHorizonUnknown)
    };

    public static Horizon Parse(string? label) => (label ?? "3M").Trim().ToUpperInvariant() switch
    {
        "1M" => Horizon.OneMonth,
        "3M" => Horizon.ThreeMonths,
        "6M" => Horizon.SixMonths,
        "12M" => Horizon.TwelveMonths,
        _ => throw new DomainException(DomainErrors.CurrencyHorizonUnknown)
    };
}

/// <summary>One currency's current composite strength plus the per-driver breakdown that produced it.</summary>
public sealed record CurrencyStrengthScore(
    Currency Currency,
    double Composite,
    IReadOnlyList<DriverScore> Breakdown,
    DataConfidence Confidence);

/// <summary>
/// The full ranked table for one point in time — the base layer under the forward matrix. Ordered
/// strongest→weakest with a deterministic ISO-code tie-break; <see cref="AsOf"/> is passed in, never read
/// from the clock.
/// </summary>
public sealed class CurrencyStrengthRanking
{
    private readonly Dictionary<string, int> _rankByCode;

    public IReadOnlyList<CurrencyStrengthScore> Scores { get; }
    public DateTimeOffset AsOf { get; }

    public CurrencyStrengthRanking(IReadOnlyList<CurrencyStrengthScore> scores, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(scores);
        if (scores.Count == 0) throw new DomainException(DomainErrors.CurrencyPanelEmpty);
        Scores = scores;
        AsOf = asOf;
        _rankByCode = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < scores.Count; i++)
            _rankByCode[scores[i].Currency.Code] = i + 1;
    }

    /// <summary>1-based rank of a currency (1 = strongest).</summary>
    public int Rank(Currency currency) =>
        _rankByCode.TryGetValue(currency.Code, out var rank) ? rank : 0;

    public CurrencyStrengthScore Strongest => Scores[0];
    public CurrencyStrengthScore Weakest => Scores[^1];
}

/// <summary>One currency's projected score at a horizon — the current composite carried along its trajectory.</summary>
public sealed record CurrencyForecast(
    Currency Currency,
    Horizon Horizon,
    double ProjectedScore,
    double CurrentScore,
    IReadOnlyList<DriverScore> ForwardBreakdown,
    DataConfidence Confidence);

/// <summary>The forward directional bias for a cross. Dead-band keeps a tiny differential at <c>Neutral</c>.</summary>
public enum DirectionalBias
{
    Depreciate = -1,
    Neutral = 0,
    Appreciate = 1
}

/// <summary>
/// The forward view for one directional cross (e.g. EUR/USD @ 3M): its bias, a normalized conviction, the
/// projected strength differential, and the per-driver "why". A pegged/low-confidence pair is flagged.
/// </summary>
public sealed record PairOutlook(
    Currency Base,
    Currency Quote,
    Horizon Horizon,
    DirectionalBias Bias,
    double Conviction,
    double ProjectedDifferential,
    IReadOnlyList<DriverScore> WhyBreakdown,
    DataConfidence Confidence,
    bool Pegged);

/// <summary>
/// All ordered directional crosses for one horizon (N×(N−1) for a universe of N). Lookup by (base, quote);
/// the strongest-vs-weakest pick is the headline "buy strongest / sell weakest" call.
/// </summary>
public sealed class PairOutlookMatrix
{
    private readonly Dictionary<(string, string), PairOutlook> _byPair;

    public IReadOnlyList<PairOutlook> Pairs { get; }
    public Horizon Horizon { get; }
    public DateTimeOffset AsOf { get; }

    public PairOutlookMatrix(IReadOnlyList<PairOutlook> pairs, Horizon horizon, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        Pairs = pairs;
        Horizon = horizon;
        AsOf = asOf;
        _byPair = new Dictionary<(string, string), PairOutlook>();
        foreach (var pair in pairs)
            _byPair[(pair.Base.Code, pair.Quote.Code)] = pair;
    }

    public PairOutlook? For(Currency @base, Currency quote) =>
        _byPair.TryGetValue((@base.Code, quote.Code), out var outlook) ? outlook : null;
}
