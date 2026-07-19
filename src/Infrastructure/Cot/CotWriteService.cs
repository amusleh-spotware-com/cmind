using Core.Cot;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Cot;

/// <summary>
/// The append-only write side of the COT module: upserts the contract-market catalog and persists each weekly
/// report exactly once. Idempotent — re-ingesting the same (market, kind, combined, report-date) is a no-op,
/// so overlapping or replayed cycles never duplicate a snapshot. Markets are created on first sight (group
/// <see cref="CotContractGroup.Other"/>, unmapped); a curated seed row keeps its group and symbol mapping.
/// </summary>
public sealed class CotWriteService(DataContext db)
{
    /// <summary>Persists a source report if not already stored; returns true when a new report was written.</summary>
    public async Task<bool> IngestAsync(CotSourceReport source, CancellationToken ct)
    {
        var code = new ContractMarketCode(source.ContractCode);
        var market = await EnsureMarketAsync(code, source.MarketName, source.Exchange, ct);

        var reportDate = source.ReportDate.ToUniversalTime();
        var exists = await db.CotReports.AsNoTracking().AnyAsync(
            r => r.ContractCodeValue == code.Value
                 && r.Kind == source.Kind
                 && r.Combined == source.Combined
                 && r.ReportDate == reportDate, ct);
        if (exists) return false;

        var report = CotReport.Create(
            market.Id, code, source.MarketName, source.Kind, source.Combined,
            reportDate, ReleaseInstant(reportDate), source.OpenInterest, source.OpenInterestChange);

        foreach (var category in source.Categories)
        {
            if (!source.Kind.Reports(category.Category)) continue;
            report.AddPosition(
                category.Category,
                new CotPositions(category.Long, category.Short, category.Spread),
                category.TradersLong,
                category.TradersShort);
        }

        db.CotReports.Add(report);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<CotMarket> EnsureMarketAsync(
        ContractMarketCode code, string name, string exchange, CancellationToken ct)
    {
        var market = await db.CotMarkets.FirstOrDefaultAsync(m => m.ContractCodeValue == code.Value, ct);
        if (market is not null) return market;

        market = CotMarket.Create(code, name, exchange, CotContractGroup.Other, null);
        db.CotMarkets.Add(market);
        await db.SaveChangesAsync(ct);
        return market;
    }

    /// <summary>
    /// When the Tuesday snapshot becomes public — the CFTC releases it the following Friday ~15:30 ET
    /// (≈20:30 UTC). Used as the point-in-time anchor so a backtest never sees a report before its release.
    /// </summary>
    private static DateTimeOffset ReleaseInstant(DateTimeOffset reportDate)
        => reportDate.Date.AddDays(3).AddHours(20).AddMinutes(30).ToUniversalTimeOffset();
}

internal static class CotDateExtensions
{
    public static DateTimeOffset ToUniversalTimeOffset(this DateTime value)
        => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
