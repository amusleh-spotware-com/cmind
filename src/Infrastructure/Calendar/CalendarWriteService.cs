using Core.Calendar;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Calendar;

/// <summary>
/// Idempotent, append-only ingest into the calendar schema. Every write goes through the aggregate
/// (schedule → release → revise → reschedule) so the append-only + monotonic-KnownAt invariants hold, and
/// re-ingesting the same print is a no-op — safe to retry and safe to run the worker more than once.
/// </summary>
public sealed class CalendarWriteService(DataContext db, TimeProvider timeProvider)
{
    /// <summary>Creates the series on first sight (keyed by series code); returns the tracked aggregate.</summary>
    public async Task<EconomicSeries> UpsertSeriesAsync(
        SeriesCode code,
        CountryCode country,
        string name,
        MarketMovingCategory category,
        ReleaseCadence cadence,
        double impactPrior,
        string sourceName,
        string sourceSeriesId,
        CancellationToken ct)
    {
        var existing = await db.CalendarSeries.FirstOrDefaultAsync(x => x.SeriesCodeValue == code.Value, ct);
        if (existing is not null) return existing;

        var series = EconomicSeries.Create(
            code, country, name, category, cadence, impactPrior, sourceName, sourceSeriesId);
        db.CalendarSeries.Add(series);
        await db.SaveChangesAsync(ct);
        return series;
    }

    /// <summary>Applies a source-fetched release/revision idempotently: schedules the event if new, records the
    /// first actual, or appends a revision only when the value actually changed and the fact is newer.</summary>
    public async Task IngestReleaseAsync(EconomicSeries series, SourceReleaseItem item, CancellationToken ct)
    {
        var effectiveAt = item.EffectiveAt.ToUniversalTime();
        var now = timeProvider.GetUtcNow();

        var economicEvent = await db.EconomicEvents
            .Include(x => x.Revisions)
            .FirstOrDefaultAsync(x => x.SeriesId == series.Id && x.EffectiveAt == effectiveAt, ct);

        if (economicEvent is null)
        {
            economicEvent = series.ScheduleRelease(ReleaseWindow.Exact(effectiveAt), "UTC", now);
            db.EconomicEvents.Add(economicEvent);
        }

        var impact = ImpactModel.Score(new ImpactInputs(series.ImpactPrior, 0, 0));

        if (item.Actual is { } actual)
        {
            if (!economicEvent.Released)
            {
                economicEvent.Release(
                    actual, forecast: null, item.Previous, impact, item.Unit, item.SourceRef, item.KnownAt,
                    earlyRelease: item.KnownAt < effectiveAt);
            }
            else if (economicEvent.LatestRevision is { } latest
                     && latest.Actual != actual
                     && item.KnownAt >= latest.KnownAt)
            {
                economicEvent.Revise(actual, null, item.Previous, impact, item.Unit, item.SourceRef, item.KnownAt);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Schedules a future release, or records a schedule move if the source shifted a known event.</summary>
    public async Task IngestScheduleAsync(EconomicSeries series, SourceScheduleItem item, CancellationToken ct)
    {
        var instant = item.Window.Instant;
        var now = timeProvider.GetUtcNow();
        var impact = ImpactModel.Score(new ImpactInputs(series.ImpactPrior, 0, 0));

        var existing = await db.EconomicEvents
            .Include(x => x.Revisions)
            .FirstOrDefaultAsync(x => x.SeriesId == series.Id && x.EffectiveAt == instant, ct);

        if (existing is null)
        {
            db.EconomicEvents.Add(EconomicEvent.Schedule(
                series.Id, series.Code, series.Country, item.Window, item.SourceTimeZone, impact, now));
            await db.SaveChangesAsync(ct);
        }
    }
}
