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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var calendar = options.CurrentValue.Calendar;
        if (!calendar.IngestionEnabled) return;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        if (await db.AppSettings.AnyAsync(s => s.Key == CompletedSetting, stoppingToken)) return;

        var backfiller = scope.ServiceProvider.GetRequiredService<CalendarBackfiller>();
        var sourcesByName = sources.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

        var seeded = await backfiller.SeedCoreSeriesAsync(stoppingToken);
        foreach (var series in seeded)
        {
            if (!sourcesByName.TryGetValue(series.SourceName, out var source)) continue;
            try
            {
                await backfiller.BackfillAsync(series, source, calendar.BackfillYears, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                // One source/series failing must not abort the rest of the warm-up.
            }
        }

        db.AppSettings.Add(AppSetting.Create(
            CompletedSetting, timeProvider.GetUtcNow().ToString("O"), timeProvider.GetUtcNow()));
        await db.SaveChangesAsync(stoppingToken);
    }
}
