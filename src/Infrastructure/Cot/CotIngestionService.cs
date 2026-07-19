using Core.Cot;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Infrastructure.Cot;

/// <summary>
/// Background ingestion for the COT feed: each cycle it seeds the curated market catalog then pulls the six
/// CFTC report variants for the tracked markets and writes them append-only. On the first run (empty store)
/// it reaches back <see cref="CotOptions.BackfillYears"/>; afterwards it re-syncs the recent
/// <see cref="CotOptions.ReconcileLookbackWeeks"/> to catch late-published revisions. Gated on
/// <see cref="CotOptions.IngestionEnabled"/> and the runtime COT enablement; writes are idempotent so
/// re-running never duplicates a report, and a source fault degrades that source only.
/// </summary>
public sealed class CotIngestionService(
    IServiceScopeFactory scopeFactory,
    IEnumerable<ICotSource> sources,
    IOptionsMonitor<AppOptions> options,
    TimeProvider timeProvider)
    : BackgroundService
{
    private static readonly (CotReportKind Kind, bool Combined)[] Variants =
    [
        (CotReportKind.Legacy, false), (CotReportKind.Legacy, true),
        (CotReportKind.Disaggregated, false), (CotReportKind.Disaggregated, true),
        (CotReportKind.Tff, false), (CotReportKind.Tff, true)
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.CurrentValue.Cot.IngestionEnabled) return;

        var source = sources.FirstOrDefault();
        if (source is null) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(source, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                // Ingestion never shares a failure domain with reads; swallow and retry next cycle.
            }

            try
            {
                await Task.Delay(options.CurrentValue.Cot.PollInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunCycleAsync(ICotSource source, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        // Honour the white-label / owner gate at runtime, re-checked each cycle so an owner toggle takes effect live.
        var featureGate = scope.ServiceProvider.GetRequiredService<Core.Features.IFeatureGate>();
        if (!CotEnablement.IsEnabled(options.CurrentValue.Branding, featureGate)) return;

        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var writer = scope.ServiceProvider.GetRequiredService<CotWriteService>();
        var health = scope.ServiceProvider.GetRequiredService<CotHealthStore>();

        await CotSeedData.EnsureSeededAsync(db, ct);

        var cot = options.CurrentValue.Cot;
        var now = timeProvider.GetUtcNow();
        var hasHistory = await db.CotReports.AsNoTracking().AnyAsync(ct);
        var since = hasHistory
            ? now - TimeSpan.FromDays(7 * Math.Max(1, cot.ReconcileLookbackWeeks))
            : now - TimeSpan.FromDays(365 * Math.Max(1, cot.BackfillYears));

        var codes = CotSeedData.TrackedCodes;
        var anySuccess = false;
        try
        {
            foreach (var (kind, combined) in Variants)
            {
                var reports = await source.FetchAsync(kind, combined, since, codes, ct);
                foreach (var report in reports)
                    await writer.IngestAsync(report, ct);
                anySuccess = true;
            }
        }
        finally
        {
            if (anySuccess) await health.RecordSuccessAsync(source.Name, ct);
            else await health.RecordFailureAsync(source.Name, ct);
        }
    }
}
