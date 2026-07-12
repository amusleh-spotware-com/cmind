using Core;
using Core.Calendar;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Calendar;

/// <summary>
/// The read side of the calendar (<see cref="IEconomicCalendar"/>) over Postgres. Every read honours the
/// query's <c>AsOf</c> point-in-time anchor: the value shown for an event is the most recent revision known
/// at that instant, never a later revision — so a backtested news rule sees exactly what was known then.
/// Reads never touch external HTTP; a fault degrades to a conservative, freshness-stamped answer.
/// </summary>
public sealed class EconomicCalendarReader(
    DataContext db,
    IOptionsMonitor<AppOptions> options,
    IEnumerable<ICalendarSource> sources,
    INewsWindowPolicy newsWindowPolicy,
    TimeProvider timeProvider)
    : IEconomicCalendar
{
    private static readonly DateTimeOffset FarFuture = DateTimeOffset.MaxValue;

    public async Task<IReadOnlyList<CalendarEventView>> GetEventsAsync(CalendarQuery query, CancellationToken ct)
    {
        var q = db.EconomicEvents.Include(x => x.Revisions).AsNoTracking().AsQueryable();

        if (query.From is { } from) q = q.Where(x => x.EffectiveAt >= from);
        if (query.To is { } to) q = q.Where(x => x.EffectiveAt <= to);
        if (query.Countries is { Count: > 0 } countries)
        {
            var upper = countries.Select(c => c.ToUpperInvariant()).ToArray();
            q = q.Where(x => upper.Contains(x.CountryValue));
        }

        if (query.Series is { Count: > 0 } series)
        {
            var upper = series.Select(s => s.ToUpperInvariant()).ToArray();
            q = q.Where(x => upper.Contains(x.SeriesCodeValue));
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.ToUpperInvariant();
            q = q.Where(x => x.SeriesCodeValue.Contains(keyword));
        }

        var events = await q.OrderBy(x => x.EffectiveAt).Take(Math.Max(1, query.Limit) * 4).ToListAsync(ct);
        var asOf = query.AsOf;

        var views = new List<CalendarEventView>();
        foreach (var economicEvent in events)
        {
            if (asOf is { } pit && !economicEvent.Revisions.Any(r => r.KnownAt <= pit)) continue;
            var view = Project(economicEvent, asOf, null);
            if (query.MinImpact is { } min && view.Impact < min) continue;
            if (query.Currencies is { Count: > 0 } currencies && !MatchesCurrencies(economicEvent, currencies)) continue;
            views.Add(view);
            if (views.Count >= query.Limit) break;
        }

        return views;
    }

    public async Task<CalendarEventView?> GetEventAsync(
        CalendarEventId id, IReadOnlyList<string>? watchlist, DateTimeOffset? asOf, CancellationToken ct)
    {
        var economicEvent = await db.EconomicEvents.Include(x => x.Revisions).AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return economicEvent is null ? null : Project(economicEvent, asOf, watchlist, includeChain: true);
    }

    public async Task<IReadOnlyList<SurprisePoint>> GetSurprisesAsync(
        SeriesCode series, int count, DateTimeOffset? asOf, CancellationToken ct)
    {
        var events = await db.EconomicEvents.Include(x => x.Revisions).AsNoTracking()
            .Where(x => x.SeriesCodeValue == series.Value)
            .OrderByDescending(x => x.EffectiveAt)
            .Take(Math.Max(1, count) * 2)
            .ToListAsync(ct);

        events.Reverse();
        var actuals = new List<decimal>();
        var points = new List<SurprisePoint>();
        foreach (var economicEvent in events)
        {
            var revision = Resolve(economicEvent, asOf);
            if (revision?.Actual is not { } actual) continue;

            var stdDev = RollingStdDev(actuals);
            var surprise = Surprise.From(actual, revision.Forecast, stdDev);
            points.Add(new SurprisePoint(
                economicEvent.EffectiveAt, actual, revision.Forecast, revision.Previous, surprise.ZScore));
            actuals.Add(actual);
        }

        if (points.Count > count) points = points.GetRange(points.Count - count, count);
        return points;
    }

    public async Task<IReadOnlyList<SeriesCatalogEntry>> GetSeriesAsync(CalendarQuery query, CancellationToken ct)
    {
        var q = db.CalendarSeries.AsNoTracking().AsQueryable();
        if (query.Countries is { Count: > 0 } countries)
        {
            var upper = countries.Select(c => c.ToUpperInvariant()).ToArray();
            q = q.Where(x => upper.Contains(x.CountryValue));
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.ToUpperInvariant();
            q = q.Where(x => x.SeriesCodeValue.Contains(keyword) || x.Name.ToUpper().Contains(keyword));
        }

        var series = await q.OrderBy(x => x.SeriesCodeValue).ToListAsync(ct);
        return series
            .Select(s => new SeriesCatalogEntry(
                s.SeriesCodeValue, s.CountryValue, s.Name, s.Category, s.Cadence, s.DefaultImpact, s.SourceName))
            .ToList();
    }

    public async Task<CalendarEventView?> GetNextForSymbolAsync(
        Symbol symbol, ImpactLevel minImpact, DateTimeOffset now, CancellationToken ct)
    {
        var events = await db.EconomicEvents.Include(x => x.Revisions).AsNoTracking()
            .Where(x => x.EffectiveAt >= now)
            .OrderBy(x => x.EffectiveAt)
            .Take(200)
            .ToListAsync(ct);

        foreach (var economicEvent in events)
        {
            if (!CurrencyExposure.Affects(economicEvent.Country, symbol)) continue;
            var view = Project(economicEvent, null, null);
            if (view.Impact < minImpact) continue;
            return view;
        }

        return null;
    }

    public async Task<BlackoutResult> GetBlackoutAsync(
        Symbol symbol, DateTimeOffset at, NewsWindowRule rule, CancellationToken ct)
    {
        try
        {
            var windowStart = at - rule.Before;
            var windowEnd = at + rule.After;
            var events = await db.EconomicEvents.Include(x => x.Revisions).AsNoTracking()
                .Where(x => x.EffectiveAt >= windowStart && x.EffectiveAt <= windowEnd)
                .ToListAsync(ct);

            var snapshots = events.Select(e => e.SnapshotAsOf(at));
            return newsWindowPolicy.Evaluate(symbol, at, rule, snapshots);
        }
        catch (Exception) when (options.CurrentValue.Calendar.BlackoutFailClosed)
        {
            // Fail-safe: a data/DB fault must never silently green-light trading through a release.
            return new BlackoutResult(true, null, at - rule.Before, at + rule.After);
        }
    }

    public async Task<IReadOnlyList<Symbol>> GetAffectedSymbolsAsync(
        CalendarEventId id, IReadOnlyList<string> watchlist, CancellationToken ct)
    {
        var economicEvent = await db.EconomicEvents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (economicEvent is null) return [];
        return CurrencyExposure.AffectedSymbols(economicEvent.Country, watchlist.Select(s => new Symbol(s)));
    }

    public Task<IReadOnlyList<SourceHealth>> GetHealthAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        IReadOnlyList<SourceHealth> health = sources
            .Select(s => new SourceHealth(s.Name, now, CircuitOpen: false, TrackedSeries: 0, Stale: false))
            .ToList();
        return Task.FromResult(health);
    }

    private static EventRevision? Resolve(EconomicEvent economicEvent, DateTimeOffset? asOf) =>
        asOf is { } pit ? economicEvent.RevisionAsOf(pit) : economicEvent.LatestRevision;

    private CalendarEventView Project(
        EconomicEvent economicEvent, DateTimeOffset? asOf, IReadOnlyList<string>? watchlist, bool includeChain = false)
    {
        var revision = Resolve(economicEvent, asOf);
        var impactLevel = economicEvent.ImpactLevelAsOf(asOf ?? FarFuture);
        var surprise = Surprise.From(revision?.Actual, revision?.Forecast, 1);

        var chain = includeChain
            ? economicEvent.Revisions
                .Where(r => asOf is not { } pit || r.KnownAt <= pit)
                .Select(r => new RevisionView(
                    r.Sequence, r.KnownAt, r.Kind, r.Actual, r.Forecast, r.Previous, r.ImpactScore,
                    r.ImpactLevel, r.ImpactModelVersion, r.Unit, r.RescheduledInstant))
                .ToList()
            : (IReadOnlyList<RevisionView>)[];

        var affected = watchlist is { Count: > 0 }
            ? CurrencyExposure.AffectedSymbols(economicEvent.Country, watchlist.Select(s => new Symbol(s)))
                .Select(s => s.Value).ToList()
            : (IReadOnlyList<string>)[];

        return new CalendarEventView
        {
            Id = economicEvent.Id,
            SeriesCode = economicEvent.SeriesCodeValue,
            Country = economicEvent.CountryValue,
            EffectiveAt = economicEvent.EffectiveAt,
            SourceTimeZone = economicEvent.SourceTimeZone,
            Precision = economicEvent.Precision,
            Impact = impactLevel,
            ImpactScore = revision?.ImpactScore ?? 0,
            Released = revision is { Actual: not null },
            Actual = revision?.Actual,
            Forecast = revision?.Forecast,
            Previous = revision?.Previous,
            SurpriseZScore = surprise.ZScore,
            Revisions = chain,
            AffectedSymbols = affected
        };
    }

    private static bool MatchesCurrencies(EconomicEvent economicEvent, IReadOnlyList<string> currencies)
    {
        var wanted = currencies.Select(c => c.ToUpperInvariant());
        var eventCurrencies = CurrencyExposure.CurrenciesOf(economicEvent.Country).Select(c => c.Value);
        return eventCurrencies.Intersect(wanted).Any();
    }

    private static double RollingStdDev(IReadOnlyList<decimal> values)
    {
        if (values.Count < 2) return 0;
        var window = values.Count > 12 ? values.Skip(values.Count - 12).ToArray() : values.ToArray();
        var mean = window.Average();
        var variance = window.Sum(v => (double)((v - mean) * (v - mean))) / window.Length;
        return Math.Sqrt(variance);
    }
}
