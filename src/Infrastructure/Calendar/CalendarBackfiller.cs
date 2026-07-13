using Core.Calendar;

namespace Infrastructure.Calendar;

/// <summary>
/// Seeds the core-series catalog and pulls their deep history from a source, append-only. Both operations are
/// idempotent (upsert by code, ingest by (series, instant)) so a re-run — or resuming after an interruption —
/// never duplicates. Chunked by year so a long backfill is bounded and resumable.
/// </summary>
public sealed class CalendarBackfiller(CalendarWriteService writer, TimeProvider timeProvider)
{
    /// <summary>Ensures every core series exists in the catalog; returns the tracked aggregates.</summary>
    public async Task<IReadOnlyList<EconomicSeries>> SeedCoreSeriesAsync(CancellationToken ct)
    {
        var seeded = new List<EconomicSeries>(CalendarSeedData.CoreSeries.Count);
        foreach (var seed in CalendarSeedData.CoreSeries)
        {
            seeded.Add(await writer.UpsertSeriesAsync(
                seed.Code, seed.Country, seed.Name, seed.Category, seed.Cadence,
                seed.ImpactPrior, seed.SourceName, seed.SourceSeriesId, ct));
        }

        return seeded;
    }

    /// <summary>
    /// Backfills the schedule for one series across recent history and the forward horizon — the keyless
    /// central-bank meeting dates the value sources do not carry, so past and upcoming decisions are both
    /// listed out of the box (history browsing works without any API key). Idempotent (ingest by instant).
    /// </summary>
    public async Task BackfillScheduleAsync(
        EconomicSeries series, ICalendarSource source, int years, int horizonDays, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var from = now.AddYears(-Math.Max(1, years));
        var to = now.AddDays(Math.Max(1, horizonDays));

        var scheduled = await source.FetchScheduleAsync(series.SourceSeriesId, from, to, ct);
        foreach (var item in scheduled)
            await writer.IngestScheduleAsync(series, item, ct);
    }

    /// <summary>Backfills up to <paramref name="years"/> of history for one series, one calendar year at a time.</summary>
    public async Task BackfillAsync(EconomicSeries series, ICalendarSource source, int years, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var earliest = now.AddYears(-Math.Max(1, years));

        for (var yearStart = new DateTimeOffset(earliest.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
             yearStart < now;
             yearStart = yearStart.AddYears(1))
        {
            ct.ThrowIfCancellationRequested();
            var yearEnd = yearStart.AddYears(1);
            var to = yearEnd < now ? yearEnd : now;

            var releases = await source.FetchReleasesAsync(series.SourceSeriesId, yearStart, to, ct);
            foreach (var release in releases)
                await writer.IngestReleaseAsync(series, release, ct);
        }
    }
}
