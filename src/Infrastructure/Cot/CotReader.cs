using Core;
using Core.Cot;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Cot;

/// <summary>
/// The read side of the COT feature (<see cref="ICotReports"/>) over Postgres. Every read honours the
/// <c>asOf</c> point-in-time anchor: only reports whose <see cref="CotReport.KnownAt"/> release instant is at
/// or before <c>asOf</c> are visible, so a backtested positioning signal sees exactly what was public then
/// (no look-ahead). Reads never touch external HTTP.
/// </summary>
public sealed class CotReader(
    DataContext db, IOptionsMonitor<AppOptions> options, CotHealthStore healthStore) : ICotReports
{
    public async Task<IReadOnlyList<CotMarketView>> GetMarketsAsync(
        CotContractGroup? group, string? keyword, CancellationToken ct)
    {
        var q = db.CotMarkets.AsNoTracking().AsQueryable();
        if (group is { } g) q = q.Where(m => m.Group == g);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword}%";
            q = q.Where(m => EF.Functions.ILike(m.Name, pattern) || EF.Functions.ILike(m.ContractCodeValue, pattern));
        }

        var markets = await q.OrderBy(m => m.Name).ToListAsync(ct);
        return markets
            .Select(m => new CotMarketView(
                m.Id, m.ContractCodeValue, m.Name, m.Exchange, m.Group, m.MappedSymbolValue))
            .ToList();
    }

    public async Task<CotReportView?> GetLatestAsync(
        ContractMarketCode code, CotReportKind kind, bool combined, DateTimeOffset? asOf, CancellationToken ct)
    {
        var window = await LoadWindowAsync(code.Value, kind, combined, asOf, null, ct);
        if (window.Count == 0) return null;
        return ProjectReport(window[^1], SpeculatorNets(window), asOf, ct);
    }

    public async Task<IReadOnlyList<CotHistoryPoint>> GetHistoryAsync(
        ContractMarketCode code, CotReportKind kind, bool combined,
        DateTimeOffset from, DateTimeOffset to, DateTimeOffset? asOf, CancellationToken ct)
    {
        var lookback = LookbackWeeks;
        var reports = await Query(code.Value, kind, combined, asOf)
            .Where(r => r.ReportDate >= from && r.ReportDate <= to)
            .OrderBy(r => r.ReportDate)
            .ToListAsync(ct);
        if (reports.Count == 0) return [];

        var nets = new List<long>(reports.Count);
        var points = new List<CotHistoryPoint>(reports.Count);
        foreach (var report in reports)
        {
            var net = report.SpeculatorNet;
            nets.Add(net);
            var lookbackNets = nets.Count > lookback ? nets.GetRange(nets.Count - lookback, lookback) : nets;
            double? index = lookbackNets.Count >= 2 ? CotIndexCalculator.Index(lookbackNets) : null;
            points.Add(new CotHistoryPoint(
                report.ReportDate, report.OpenInterest, net, index, CategoryViews(report)));
        }

        return points;
    }

    public async Task<CotReportView?> GetReportAsync(CotReportId id, DateTimeOffset? asOf, CancellationToken ct)
    {
        var report = await db.CotReports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (report is null) return null;
        if (asOf is { } pit && report.KnownAt > pit) return null;

        var window = await LoadWindowAsync(
            report.ContractCodeValue, report.Kind, report.Combined, asOf, report.ReportDate, ct);
        return ProjectReport(report, SpeculatorNets(window), asOf, ct);
    }

    public Task<IReadOnlyList<CotSourceHealth>> GetHealthAsync(CancellationToken ct)
        => healthStore.GetAllAsync(["CFTC"], ct);

    private int LookbackWeeks => Math.Max(2, options.CurrentValue.Cot.CotIndexLookbackWeeks);

    private IQueryable<CotReport> Query(string code, CotReportKind kind, bool combined, DateTimeOffset? asOf)
    {
        var q = db.CotReports.AsNoTracking()
            .Where(r => r.ContractCodeValue == code && r.Kind == kind && r.Combined == combined);
        if (asOf is { } pit) q = q.Where(r => r.KnownAt <= pit);
        return q;
    }

    /// <summary>Loads the newest report at/upto <paramref name="upto"/> plus the prior lookback window for the index.</summary>
    private async Task<List<CotReport>> LoadWindowAsync(
        string code, CotReportKind kind, bool combined, DateTimeOffset? asOf, DateTimeOffset? upto, CancellationToken ct)
    {
        var q = Query(code, kind, combined, asOf);
        if (upto is { } cap) q = q.Where(r => r.ReportDate <= cap);
        var reports = await q.OrderByDescending(r => r.ReportDate).Take(LookbackWeeks).ToListAsync(ct);
        reports.Reverse();
        return reports;
    }

    private static List<long> SpeculatorNets(IReadOnlyList<CotReport> window)
        => window.Select(r => r.SpeculatorNet).ToList();

    private CotReportView ProjectReport(
        CotReport report, List<long> speculatorNets, DateTimeOffset? asOf, CancellationToken ct)
    {
        _ = ct;
        double? index = speculatorNets.Count >= 2 ? CotIndexCalculator.Index(speculatorNets) : null;
        var extreme = index is { } i ? CotIndexCalculator.Classify(i) : CotExtreme.None;
        var stale = IsStale(report, asOf);

        return new CotReportView
        {
            Id = report.Id,
            ContractCode = report.ContractCodeValue,
            MarketName = report.MarketName,
            Kind = report.Kind,
            Combined = report.Combined,
            ReportDate = report.ReportDate,
            KnownAt = report.KnownAt,
            OpenInterest = report.OpenInterest,
            OpenInterestChange = report.OpenInterestChange,
            Categories = CategoryViews(report),
            CotIndex = index,
            Extreme = extreme,
            Stale = stale
        };
    }

    private bool IsStale(CotReport report, DateTimeOffset? asOf)
    {
        var anchor = asOf ?? DateTimeOffset.MaxValue;
        if (anchor == DateTimeOffset.MaxValue) return false;
        return anchor - report.ReportDate > options.CurrentValue.Cot.SourceStaleAfter;
    }

    private static List<CotCategoryView> CategoryViews(CotReport report)
    {
        var oi = report.OpenInterest;
        return report.Kind.Categories()
            .Select(report.PositionFor)
            .Where(p => p is not null)
            .Select(p => new CotCategoryView(
                p!.Category, p.Long, p.Short, p.Spread, p.Net,
                p.Positions.LongPercentOf(oi), p.Positions.ShortPercentOf(oi),
                p.TradersLong, p.TradersShort))
            .ToList();
    }
}
