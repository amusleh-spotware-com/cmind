using Core.Calendar;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Infrastructure.Calendar;

/// <summary>
/// Background ingestion for the calendar: each cycle it re-syncs recent releases for every tracked series
/// from its primary source and writes them append-only. Gated on <see cref="CalendarOptions.IngestionEnabled"/>
/// — off by default, so the domain and lazy read side work without any external calls. Writes are idempotent,
/// so re-running (or overlapping runs) never duplicate a print; a per-source fault degrades that source only.
/// </summary>
public sealed class CalendarIngestionService(
    IServiceScopeFactory scopeFactory,
    IEnumerable<ICalendarSource> sources,
    IOptionsMonitor<AppOptions> options,
    TimeProvider timeProvider)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var calendar = options.CurrentValue.Calendar;
        if (!calendar.IngestionEnabled) return;

        var sourcesByName = sources.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(sourcesByName, stoppingToken);
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
                await Task.Delay(options.CurrentValue.Calendar.ReleasePollInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunCycleAsync(
        Dictionary<string, ICalendarSource> sourcesByName, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var writer = scope.ServiceProvider.GetRequiredService<CalendarWriteService>();
        var health = scope.ServiceProvider.GetRequiredService<CalendarHealthStore>();

        var now = timeProvider.GetUtcNow();
        var from = now - TimeSpan.FromDays(options.CurrentValue.Calendar.ReconcileLookbackDays);

        var series = await db.CalendarSeries.AsNoTracking().ToListAsync(ct);
        foreach (var s in series)
        {
            if (!sourcesByName.TryGetValue(s.SourceName, out var source)) continue;

            try
            {
                var releases = await source.FetchReleasesAsync(s.SourceSeriesId, from, now, ct);
                foreach (var release in releases)
                    await writer.IngestReleaseAsync(s, release, ct);
                await health.RecordSuccessAsync(source.Name, ct);
            }
            catch (Exception)
            {
                // One source down degrades only its coverage; keep syncing the rest.
                await health.RecordFailureAsync(source.Name, ct);
            }
        }
    }
}
