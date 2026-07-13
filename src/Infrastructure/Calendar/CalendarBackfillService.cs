using Core;
using Core.Calendar;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Infrastructure.Calendar;

/// <summary>
/// One-time proactive warm-up: on first enable it seeds the core-series catalog and backfills their history
/// from the matching source, so the common case is populated without waiting for a user miss. Gated on
/// <see cref="CalendarOptions.IngestionEnabled"/>, guarded by a run-once marker in app settings, and
/// idempotent throughout — safe to run again, and a per-source fault degrades only that series.
/// </summary>
public sealed class CalendarBackfillService(
    IServiceScopeFactory scopeFactory,
    IEnumerable<ICalendarSource> sources,
    IOptionsMonitor<AppOptions> options,
    TimeProvider timeProvider)
    : BackgroundService
{
    private const string CompletedSetting = "calendar.backfill.completed";
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.CurrentValue.Calendar.IngestionEnabled) return;

        // The warm-up races app startup — migrations may not have created the schema yet. A DB/transient
        // fault must never fault this BackgroundService (that would stop the host); retry until the schema
        // is ready, then run once and return. Cancellation ends it cleanly.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await TryWarmUpAsync(stoppingToken)) return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                // DB not migrated yet, or a transient fault — back off and retry.
            }

            try
            {
                await Task.Delay(RetryDelay, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>One warm-up attempt. Returns true when the work is done (or the calendar is gated off / already
    /// warmed), false to signal a retry is warranted. Throws only on cancellation or an unexpected fault.</summary>
    private async Task<bool> TryWarmUpAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        // Honour the white-label / owner gate: a calendar-off deployment must not warm up even though
        // ingestion is on by default.
        var featureGate = scope.ServiceProvider.GetRequiredService<Core.Features.IFeatureGate>();
        if (!CalendarEnablement.IsEnabled(options.CurrentValue.Branding, featureGate)) return true;

        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        if (await db.AppSettings.AnyAsync(s => s.Key == CompletedSetting, ct)) return true;

        var calendar = options.CurrentValue.Calendar;
        var backfiller = scope.ServiceProvider.GetRequiredService<CalendarBackfiller>();
        var sourcesByName = sources.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

        var seeded = await backfiller.SeedCoreSeriesAsync(ct);
        foreach (var series in seeded)
        {
            if (!sourcesByName.TryGetValue(series.SourceName, out var source)) continue;
            try
            {
                await backfiller.BackfillScheduleAsync(
                    series, source, calendar.BackfillYears, calendar.ScheduleHorizonDays, ct);
                await backfiller.BackfillAsync(series, source, calendar.BackfillYears, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // One source/series failing must not abort the rest of the warm-up.
            }
        }

        db.AppSettings.Add(AppSetting.Create(
            CompletedSetting, timeProvider.GetUtcNow().ToString("O"), timeProvider.GetUtcNow()));
        await db.SaveChangesAsync(ct);
        return true;
    }
}
