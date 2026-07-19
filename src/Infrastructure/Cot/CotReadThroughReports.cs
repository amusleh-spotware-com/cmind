using System.Collections.Concurrent;
using Core;
using Core.Cot;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Cot;

/// <summary>
/// Per-key concurrency + refresh-throttle registry shared across scoped read-through readers, so two
/// concurrent requests for the same market never double-fetch and a market is not re-fetched more often
/// than the poll interval. In-memory (node-local) — the durable cache is the database.
/// </summary>
public sealed class CotLoadGate
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastAttempt = new();

    public SemaphoreSlim For(string key) => _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
    public DateTimeOffset LastAttempt(string key) => _lastAttempt.TryGetValue(key, out var t) ? t : DateTimeOffset.MinValue;
    public void MarkAttempt(string key, DateTimeOffset now) => _lastAttempt[key] = now;
}

/// <summary>
/// A read-through cache over <see cref="CotReader"/>. The database is the cache: the first request for a
/// market (and variant) that has no stored data fetches it from the CFTC source and persists it, then every
/// subsequent request is served straight from the database. When a new weekly report is due (the newest
/// stored report is older than a week) the next request transparently pulls the recent window and appends it
/// — so the cache stays current as data is released, without waiting for the periodic ingestion worker. The
/// fetch is throttled per market (never more than once per poll interval) and a source fault degrades to
/// serving the best cached data. All reads still ultimately come from the pure <see cref="CotReader"/>.
/// </summary>
public sealed class CotReadThroughReports(
    CotReader inner,
    ICotSource source,
    CotWriteService writer,
    CotHealthStore health,
    DataContext db,
    CotLoadGate gate,
    IOptionsMonitor<AppOptions> options,
    TimeProvider timeProvider) : ICotReports
{
    public async Task<IReadOnlyList<CotMarketView>> GetMarketsAsync(
        CotContractGroup? group, string? keyword, CancellationToken ct)
    {
        // Ensure the curated catalog exists so the market list is populated before the worker's first cycle.
        await CotSeedData.EnsureSeededAsync(db, ct);
        return await inner.GetMarketsAsync(group, keyword, ct);
    }

    public async Task<CotReportView?> GetLatestAsync(
        ContractMarketCode code, CotReportKind kind, bool combined, DateTimeOffset? asOf, CancellationToken ct)
    {
        await EnsureFreshAsync(code, kind, combined, ct);
        return await inner.GetLatestAsync(code, kind, combined, asOf, ct);
    }

    public async Task<IReadOnlyList<CotHistoryPoint>> GetHistoryAsync(
        ContractMarketCode code, CotReportKind kind, bool combined,
        DateTimeOffset from, DateTimeOffset to, DateTimeOffset? asOf, CancellationToken ct)
    {
        await EnsureFreshAsync(code, kind, combined, ct);
        return await inner.GetHistoryAsync(code, kind, combined, from, to, asOf, ct);
    }

    public Task<CotReportView?> GetReportAsync(CotReportId id, DateTimeOffset? asOf, CancellationToken ct)
        => inner.GetReportAsync(id, asOf, ct);

    public Task<IReadOnlyList<CotSourceHealth>> GetHealthAsync(CancellationToken ct)
        => inner.GetHealthAsync(ct);

    private async Task EnsureFreshAsync(ContractMarketCode code, CotReportKind kind, bool combined, CancellationToken ct)
    {
        var cot = options.CurrentValue.Cot;
        var now = timeProvider.GetUtcNow();

        var latestStored = await db.CotReports.AsNoTracking()
            .Where(r => r.ContractCodeValue == code.Value && r.Kind == kind && r.Combined == combined)
            .OrderByDescending(r => r.ReportDate)
            .Select(r => (DateTimeOffset?)r.ReportDate)
            .FirstOrDefaultAsync(ct);

        if (!NeedsLoad(latestStored, now)) return;

        var key = $"{code.Value}|{kind}|{combined}";
        // Throttle uniformly (including a market the source has no data for) so a fetch runs at most once per
        // poll interval per market — a genuinely empty market is not re-fetched on every request.
        if (now - gate.LastAttempt(key) < cot.PollInterval) return;

        var semaphore = gate.For(key);
        await semaphore.WaitAsync(ct);
        try
        {
            // Re-check under the lock — another request may have just loaded (or attempted) it.
            var stillLatest = await db.CotReports.AsNoTracking()
                .Where(r => r.ContractCodeValue == code.Value && r.Kind == kind && r.Combined == combined)
                .OrderByDescending(r => r.ReportDate)
                .Select(r => (DateTimeOffset?)r.ReportDate)
                .FirstOrDefaultAsync(ct);
            if (!NeedsLoad(stillLatest, now) || now - gate.LastAttempt(key) < cot.PollInterval) return;

            gate.MarkAttempt(key, now);
            var since = stillLatest is null
                ? now - TimeSpan.FromDays(365 * Math.Max(1, cot.BackfillYears))
                : now - TimeSpan.FromDays(7 * Math.Max(1, cot.ReconcileLookbackWeeks));

            try
            {
                var reports = await source.FetchAsync(kind, combined, since, [code.Value], ct);
                foreach (var report in reports)
                    await writer.IngestAsync(report, ct);
                await health.RecordSuccessAsync(source.Name, ct);
            }
            catch (Exception)
            {
                // Source down — serve whatever is cached; record the failure for the health view.
                await health.RecordFailureAsync(source.Name, ct);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    // Load when nothing is cached yet, or the newest cached report is old enough that a new weekly release is due.
    private static bool NeedsLoad(DateTimeOffset? latestStored, DateTimeOffset now)
        => latestStored is not { } stored || now - stored > TimeSpan.FromDays(7);
}
