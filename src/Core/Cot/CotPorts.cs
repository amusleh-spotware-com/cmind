namespace Core.Cot;

/// <summary>A read query over COT reports; all filters are optional and combine conjunctively.</summary>
public sealed record CotQuery
{
    public string? ContractCode { get; init; }
    public CotReportKind Kind { get; init; } = CotReportKind.Legacy;
    public bool Combined { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }

    /// <summary>Point-in-time anchor: return the data exactly as it was public at this instant (no look-ahead).</summary>
    public DateTimeOffset? AsOf { get; init; }

    public int Limit { get; init; } = 260;
}

/// <summary>A catalog entry describing a tracked contract market.</summary>
public sealed record CotMarketView(
    CotMarketId Id,
    string ContractCode,
    string Name,
    string Exchange,
    CotContractGroup Group,
    string? Symbol);

/// <summary>One trader category's positioning as exposed to read consumers (API/MCP/UI).</summary>
public sealed record CotCategoryView(
    CotTraderCategory Category,
    long Long,
    long Short,
    long Spread,
    long Net,
    double LongPercentOfOi,
    double ShortPercentOfOi,
    int? TradersLong,
    int? TradersShort);

/// <summary>A full point-in-time view of one weekly COT snapshot with its computed positioning gauge.</summary>
public sealed record CotReportView
{
    public required CotReportId Id { get; init; }
    public required string ContractCode { get; init; }
    public required string MarketName { get; init; }
    public required CotReportKind Kind { get; init; }
    public required bool Combined { get; init; }
    public required DateTimeOffset ReportDate { get; init; }
    public required DateTimeOffset KnownAt { get; init; }
    public required long OpenInterest { get; init; }
    public long? OpenInterestChange { get; init; }
    public IReadOnlyList<CotCategoryView> Categories { get; init; } = [];

    /// <summary>The COT index (0..100) of the speculator net position over the lookback, when enough history.</summary>
    public double? CotIndex { get; init; }

    public CotExtreme Extreme { get; init; }

    /// <summary>Freshness stamp so a consumer can tell whether the underlying source data is degraded/stale.</summary>
    public bool Stale { get; init; }
}

/// <summary>A single point on a positioning history chart for deep-history backtesting/overlay.</summary>
public sealed record CotHistoryPoint(
    DateTimeOffset ReportDate,
    long OpenInterest,
    long SpeculatorNet,
    double? CotIndex,
    IReadOnlyList<CotCategoryView> Categories);

/// <summary>Per-source freshness/coverage so a consumer can judge how far to trust the data.</summary>
public sealed record CotSourceHealth(
    string SourceName,
    DateTimeOffset? LastSuccessfulPollAt,
    bool CircuitOpen,
    int TrackedMarkets,
    bool Stale);

/// <summary>
/// The read side of the COT feature. Served from indexed read-models/cache; never touches external HTTP on
/// the hot path. Every method honours the query's <c>asOf</c> point-in-time anchor so backtested positioning
/// signals behave exactly like live.
/// </summary>
public interface ICotReports
{
    Task<IReadOnlyList<CotMarketView>> GetMarketsAsync(
        CotContractGroup? group, string? keyword, CancellationToken ct);

    Task<CotReportView?> GetLatestAsync(
        ContractMarketCode code, CotReportKind kind, bool combined, DateTimeOffset? asOf, CancellationToken ct);

    Task<IReadOnlyList<CotHistoryPoint>> GetHistoryAsync(
        ContractMarketCode code, CotReportKind kind, bool combined,
        DateTimeOffset from, DateTimeOffset to, DateTimeOffset? asOf, CancellationToken ct);

    Task<CotReportView?> GetReportAsync(CotReportId id, DateTimeOffset? asOf, CancellationToken ct);

    Task<IReadOnlyList<CotSourceHealth>> GetHealthAsync(CancellationToken ct);
}

/// <summary>A single trader-category row fetched from a primary source, before domain validation.</summary>
public sealed record CotSourceCategory(
    CotTraderCategory Category, long Long, long Short, long Spread, int? TradersLong, int? TradersShort);

/// <summary>One weekly report row fetched from a primary source (CFTC), with its full category breakdown.</summary>
public sealed record CotSourceReport(
    string ContractCode,
    string MarketName,
    string Exchange,
    CotReportKind Kind,
    bool Combined,
    DateTimeOffset ReportDate,
    long OpenInterest,
    long? OpenInterestChange,
    IReadOnlyList<CotSourceCategory> Categories);

/// <summary>
/// One primary-source connector for COT data (the CFTC public Socrata datasets). Ingest-side only — never on
/// a read path. A dead source degrades only its own coverage.
/// </summary>
public interface ICotSource
{
    string Name { get; }

    /// <summary>
    /// Fetches report rows of the given variant with a report date on or after <paramref name="since"/>.
    /// When <paramref name="contractCodes"/> is non-empty the fetch is bounded to those contract markets
    /// (server-side), keeping ingestion volume small; empty/null fetches every market.
    /// </summary>
    Task<IReadOnlyList<CotSourceReport>> FetchAsync(
        CotReportKind kind, bool combined, DateTimeOffset since,
        IReadOnlyCollection<string>? contractCodes, CancellationToken ct);
}
