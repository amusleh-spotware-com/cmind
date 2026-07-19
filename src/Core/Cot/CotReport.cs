using System.ComponentModel.DataAnnotations;
using Core.Constants;
using Core.Domain;

namespace Core.Cot;

/// <summary>
/// One weekly Commitment of Traders snapshot for a single contract market and report variant — the positions
/// as of a Tuesday, published (become public / <see cref="KnownAt"/>) the following Friday. Immutable once
/// built: a COT report is a fixed fact, so there are no state transitions, only construction with its full
/// category breakdown. <see cref="KnownAt"/> is the point-in-time anchor so backtests never see a report
/// before it was released (no look-ahead). References its market by strong id.
/// </summary>
public sealed class CotReport : AuditedEntity<CotReportId>
{
    private readonly List<CotCategoryPosition> _positions = [];

    public CotMarketId MarketId { get; private set; }

    [MaxLength(16)] public string ContractCodeValue { get; private set; } = default!;
    [MaxLength(160)] public string MarketName { get; private set; } = default!;

    public CotReportKind Kind { get; private set; }

    /// <summary>Futures-and-options combined when <c>true</c>; futures-only when <c>false</c>.</summary>
    public bool Combined { get; private set; }

    /// <summary>The Tuesday the positions were measured (UTC date anchor).</summary>
    public DateTimeOffset ReportDate { get; private set; }

    /// <summary>When the report became public — the point-in-time anchor for look-ahead-free reads.</summary>
    public DateTimeOffset KnownAt { get; private set; }

    public long OpenInterest { get; private set; }
    public long? OpenInterestChange { get; private set; }

    public IReadOnlyList<CotCategoryPosition> Positions => _positions;

    public ContractMarketCode ContractCode => new(ContractCodeValue);
    public CotReportType ReportType => new(Kind, Combined);

    private CotReport()
    {
    }

    public static CotReport Create(
        CotMarketId marketId,
        ContractMarketCode code,
        string marketName,
        CotReportKind kind,
        bool combined,
        DateTimeOffset reportDate,
        DateTimeOffset knownAt,
        long openInterest,
        long? openInterestChange)
    {
        if (openInterest < 0) throw new DomainException(DomainErrors.CotOpenInterestNegative);
        if (reportDate == default) throw new DomainException(DomainErrors.CotReportDateInvalid);

        return new CotReport
        {
            MarketId = marketId,
            ContractCodeValue = code.Value,
            MarketName = DomainGuard.AgainstNullOrWhiteSpace(marketName, DomainErrors.CotMarketNameRequired),
            Kind = kind,
            Combined = combined,
            ReportDate = reportDate.ToUniversalTime(),
            KnownAt = knownAt.ToUniversalTime(),
            OpenInterest = openInterest,
            OpenInterestChange = openInterestChange
        };
    }

    /// <summary>Adds a category breakdown row; the category must belong to this report's kind and be unique.</summary>
    public CotCategoryPosition AddPosition(
        CotTraderCategory category, CotPositions positions, int? tradersLong = null, int? tradersShort = null)
    {
        if (!Kind.Reports(category))
            throw new DomainException(DomainErrors.CotCategoryNotInReportKind);
        if (_positions.Any(p => p.Category == category))
            throw new DomainException(DomainErrors.CotCategoryDuplicate);

        var position = CotCategoryPosition.Create(category, positions, tradersLong, tradersShort);
        _positions.Add(position);
        return position;
    }

    public CotCategoryPosition? PositionFor(CotTraderCategory category)
        => _positions.FirstOrDefault(p => p.Category == category);

    /// <summary>The net position of the speculator category that headlines this report kind's COT index.</summary>
    public long SpeculatorNet => PositionFor(Kind.SpeculatorCategory())?.Net ?? 0;
}
