namespace Core.Calendar;

/// <summary>A read query over the calendar; all filters are optional and combine conjunctively.</summary>
public sealed record CalendarQuery
{
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public IReadOnlyList<string>? Countries { get; init; }
    public IReadOnlyList<string>? Currencies { get; init; }
    public IReadOnlyList<string>? Series { get; init; }
    public ImpactLevel? MinImpact { get; init; }
    public MarketMovingCategory? Category { get; init; }
    public string? Keyword { get; init; }

    /// <summary>Point-in-time anchor: return the calendar exactly as it was known at this instant (no look-ahead).</summary>
    public DateTimeOffset? AsOf { get; init; }

    public string? Cursor { get; init; }
    public int Limit { get; init; } = 200;
}

/// <summary>One revision as exposed to read consumers (API/MCP/UI).</summary>
public sealed record RevisionView(
    int Sequence,
    DateTimeOffset KnownAt,
    RevisionKind Kind,
    decimal? Actual,
    decimal? Forecast,
    decimal? Previous,
    double ImpactScore,
    ImpactLevel ImpactLevel,
    int ImpactModelVersion,
    string? Unit,
    DateTimeOffset? RescheduledInstant);

/// <summary>A point-in-time view of an event with its resolved current values and (optionally) the full chain.</summary>
public sealed record CalendarEventView
{
    public required CalendarEventId Id { get; init; }
    public required string SeriesCode { get; init; }
    public required string Country { get; init; }
    public required DateTimeOffset EffectiveAt { get; init; }
    public required string SourceTimeZone { get; init; }
    public required ReleasePrecision Precision { get; init; }
    public required ImpactLevel Impact { get; init; }
    public required double ImpactScore { get; init; }
    public required bool Released { get; init; }
    public decimal? Actual { get; init; }
    public decimal? Forecast { get; init; }
    public decimal? Previous { get; init; }
    public double SurpriseZScore { get; init; }
    public IReadOnlyList<RevisionView> Revisions { get; init; } = [];
    public IReadOnlyList<string> AffectedSymbols { get; init; } = [];

    /// <summary>Freshness stamp so a consumer can tell whether the underlying source data is degraded/stale.</summary>
    public bool Stale { get; init; }
}

/// <summary>An actual/forecast/surprise triple for backtesting news rules over deep history.</summary>
public sealed record SurprisePoint(
    DateTimeOffset EffectiveAt,
    decimal? Actual,
    decimal? Forecast,
    decimal? Previous,
    double SurpriseZScore);

/// <summary>A catalog entry describing a tracked indicator.</summary>
public sealed record SeriesCatalogEntry(
    string SeriesCode,
    string Country,
    string Name,
    MarketMovingCategory Category,
    ReleaseCadence Cadence,
    ImpactLevel DefaultImpact,
    string SourceName);

/// <summary>Per-source freshness/coverage so a consumer (or agent) can judge how far to trust the data.</summary>
public sealed record SourceHealth(
    string SourceName,
    DateTimeOffset? LastSuccessfulPollAt,
    bool CircuitOpen,
    int TrackedSeries,
    bool Stale);

/// <summary>
/// The read side of the calendar (the <c>IEconomicCalendar</c> port). Served from indexed read-models/cache;
/// never touches external HTTP on the hot path. Every method honours the query's <c>AsOf</c> point-in-time
/// anchor so backtested news rules behave exactly like live.
/// </summary>
public interface IEconomicCalendar
{
    Task<IReadOnlyList<CalendarEventView>> GetEventsAsync(CalendarQuery query, CancellationToken ct);

    Task<CalendarEventView?> GetEventAsync(
        CalendarEventId id, IReadOnlyList<string>? watchlist, DateTimeOffset? asOf, CancellationToken ct);

    Task<IReadOnlyList<SurprisePoint>> GetSurprisesAsync(
        SeriesCode series, int count, DateTimeOffset? asOf, CancellationToken ct);

    Task<IReadOnlyList<SeriesCatalogEntry>> GetSeriesAsync(CalendarQuery query, CancellationToken ct);

    Task<CalendarEventView?> GetNextForSymbolAsync(
        Symbol symbol, ImpactLevel minImpact, DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// Events affecting a symbol (country→currency→symbol) within a window — for overlaying event markers on
    /// a backtest report. Honours <paramref name="asOf"/> so the overlay is point-in-time correct (a strategy
    /// author sees exactly the events that were known during the backtested period, no look-ahead).
    /// </summary>
    Task<IReadOnlyList<CalendarEventView>> GetEventsForSymbolAsync(
        Symbol symbol, DateTimeOffset from, DateTimeOffset to, DateTimeOffset? asOf, CancellationToken ct);

    Task<BlackoutResult> GetBlackoutAsync(
        Symbol symbol, DateTimeOffset at, NewsWindowRule rule, CancellationToken ct);

    Task<IReadOnlyList<Symbol>> GetAffectedSymbolsAsync(
        CalendarEventId id, IReadOnlyList<string> watchlist, CancellationToken ct);

    Task<IReadOnlyList<SourceHealth>> GetHealthAsync(CancellationToken ct);
}

/// <summary>
/// Optional, pluggable consensus/forecast port — off by default. Primary sources do not publish the survey
/// median, so a deployment may wire a licensed feed (bring-your-own key). The event schema carries a nullable
/// forecast; the default implementation returns <c>null</c> and never fabricates a value.
/// </summary>
public interface IForecastProvider
{
    Task<decimal?> GetConsensusAsync(SeriesCode series, DateTimeOffset effectiveAt, CancellationToken ct);
}

/// <summary>A schedule row fetched from a primary source before the actual is known.</summary>
public sealed record SourceScheduleItem(
    string SourceSeriesId,
    ReleaseWindow Window,
    string SourceTimeZone);

/// <summary>A printed (or revised) value fetched from a primary source, with the instant it applies to.</summary>
public sealed record SourceReleaseItem(
    string SourceSeriesId,
    DateTimeOffset EffectiveAt,
    DateTimeOffset KnownAt,
    decimal? Actual,
    decimal? Previous,
    string? Unit,
    string? SourceRef);

/// <summary>
/// One primary-source connector (FRED, BLS, BEA, ECB…). Each lives behind its own resilient typed
/// <c>HttpClient</c>; a dead source degrades only its own coverage. Ingest-side only — never on a read path.
/// </summary>
public interface ICalendarSource
{
    string Name { get; }

    Task<IReadOnlyList<SourceScheduleItem>> FetchScheduleAsync(
        string sourceSeriesId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);

    Task<IReadOnlyList<SourceReleaseItem>> FetchReleasesAsync(
        string sourceSeriesId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}
