using Core.Constants;
using Core.Domain;

namespace Core.Calendar;

/// <summary>
/// A news-blackout rule: pause/guard around releases at or above <see cref="MinImpact"/>, from
/// <see cref="BeforeMinutes"/> before to <see cref="AfterMinutes"/> after the instant, optionally narrowed to
/// specific <see cref="Currencies"/> or <see cref="Series"/>. Drives the cBot news filter, copy-trade pause,
/// and AI risk guard through one shared, tested implementation.
/// </summary>
public sealed record NewsWindowRule
{
    public ImpactLevel MinImpact { get; }
    public int BeforeMinutes { get; }
    public int AfterMinutes { get; }
    public IReadOnlySet<string>? Currencies { get; }
    public IReadOnlySet<string>? Series { get; }

    public NewsWindowRule(
        ImpactLevel minImpact,
        int beforeMinutes,
        int afterMinutes,
        IReadOnlySet<string>? currencies = null,
        IReadOnlySet<string>? series = null)
    {
        if (beforeMinutes < 0 || afterMinutes < 0 || (beforeMinutes == 0 && afterMinutes == 0))
            throw new DomainException(DomainErrors.CalendarNewsWindowInvalid);

        MinImpact = minImpact;
        BeforeMinutes = beforeMinutes;
        AfterMinutes = afterMinutes;
        Currencies = currencies;
        Series = series;
    }

    public TimeSpan Before => TimeSpan.FromMinutes(BeforeMinutes);
    public TimeSpan After => TimeSpan.FromMinutes(AfterMinutes);
}

/// <summary>A pure, minimal projection of an event that the news-window policy evaluates against.</summary>
public readonly record struct CalendarEventSnapshot(
    CalendarEventId Id,
    SeriesCode Series,
    CountryCode Country,
    DateTimeOffset EffectiveAt,
    ImpactLevel Impact);

/// <summary>The outcome of a blackout check: whether we are inside a window and, if so, which event and edges.</summary>
public readonly record struct BlackoutResult(
    bool InBlackout,
    CalendarEventSnapshot? Trigger,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt)
{
    public static BlackoutResult Clear => new(false, null, null, null);
}

/// <summary>"Is instant T inside a blackout for symbol S?" — pure, deterministic, no external calls.</summary>
public interface INewsWindowPolicy
{
    BlackoutResult Evaluate(
        Symbol symbol,
        DateTimeOffset instant,
        NewsWindowRule rule,
        IEnumerable<CalendarEventSnapshot> candidates);
}

/// <summary>
/// The single shared news-window implementation. An event triggers a blackout when it is at/above the rule's
/// impact, matches the rule's currency/series filters, affects the symbol (country→currency→symbol), and the
/// instant lies within <c>[EffectiveAt − before, EffectiveAt + after]</c>. On overlap the earliest-starting
/// window wins, so the returned edges are stable.
/// </summary>
public sealed class NewsWindowPolicy : INewsWindowPolicy
{
    public BlackoutResult Evaluate(
        Symbol symbol,
        DateTimeOffset instant,
        NewsWindowRule rule,
        IEnumerable<CalendarEventSnapshot> candidates)
    {
        BlackoutResult best = BlackoutResult.Clear;

        foreach (var candidate in candidates)
        {
            if (candidate.Impact < rule.MinImpact) continue;
            if (rule.Series is { } series && !series.Contains(candidate.Series.Value)) continue;
            if (rule.Currencies is { } currencies && !MatchesCurrency(candidate.Country, currencies)) continue;
            if (!CurrencyExposure.Affects(candidate.Country, symbol)) continue;

            var startsAt = candidate.EffectiveAt - rule.Before;
            var endsAt = candidate.EffectiveAt + rule.After;
            if (instant < startsAt || instant > endsAt) continue;

            if (!best.InBlackout || startsAt < best.StartsAt)
                best = new BlackoutResult(true, candidate, startsAt, endsAt);
        }

        return best;
    }

    private static bool MatchesCurrency(CountryCode country, IReadOnlySet<string> currencies)
    {
        foreach (var ccy in CurrencyExposure.CurrenciesOf(country))
            if (currencies.Contains(ccy.Value))
                return true;
        return false;
    }
}
