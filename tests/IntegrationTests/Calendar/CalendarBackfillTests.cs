using Core.Calendar;
using FluentAssertions;
using Infrastructure.Calendar;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests.Calendar;

public class CalendarBackfillTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = new(2024, 3, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Effective = new(2024, 2, 13, 13, 30, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // Returns one canned release regardless of the requested window — exercises the per-year chunk loop and
    // its idempotency (the same print is fetched in several chunks but persisted once).
    private sealed class FakeCalendarSource : ICalendarSource
    {
        public string Name => "FRED";
        public int FetchCalls { get; private set; }

        public Task<IReadOnlyList<SourceScheduleItem>> FetchScheduleAsync(
            string sourceSeriesId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<SourceScheduleItem>>([]);

        public Task<IReadOnlyList<SourceReleaseItem>> FetchReleasesAsync(
            string sourceSeriesId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
        {
            FetchCalls++;
            IReadOnlyList<SourceReleaseItem> items = Effective >= from && Effective <= to
                ? [new SourceReleaseItem(sourceSeriesId, Effective, Effective.AddMinutes(1), 3.1m, 2.9m, "%", "fake")]
                : [];
            return Task.FromResult(items);
        }
    }

    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System))
            .Options);

    private async Task<DataContext> FreshAsync()
    {
        var db = CreateContext();
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE calendar.event_revision, calendar.economic_event, calendar.series CASCADE");
        return db;
    }

    private static CalendarBackfiller Backfiller(DataContext db) =>
        new(new CalendarWriteService(db, new FixedTimeProvider(Now)), new FixedTimeProvider(Now));

    [Fact]
    public async Task Seed_populates_the_core_series_catalog_idempotently()
    {
        await using var db = await FreshAsync();
        var backfiller = Backfiller(db);

        var first = await backfiller.SeedCoreSeriesAsync(CancellationToken.None);
        await backfiller.SeedCoreSeriesAsync(CancellationToken.None);

        first.Should().HaveCount(CalendarSeedData.CoreSeries.Count);
        (await db.CalendarSeries.CountAsync()).Should().Be(CalendarSeedData.CoreSeries.Count);
    }

    [Fact]
    public async Task Backfill_persists_history_and_is_idempotent_across_chunks_and_reruns()
    {
        await using var db = await FreshAsync();
        var backfiller = Backfiller(db);
        var seeded = await backfiller.SeedCoreSeriesAsync(CancellationToken.None);
        var cpi = seeded.First(s => s.SeriesCodeValue == "US.CPI");
        var source = new FakeCalendarSource();

        await backfiller.BackfillAsync(cpi, source, years: 2, CancellationToken.None);
        await backfiller.BackfillAsync(cpi, source, years: 2, CancellationToken.None);

        source.FetchCalls.Should().BeGreaterThan(1, "the backfill chunks by year");
        var events = await db.EconomicEvents.Where(e => e.SeriesId == cpi.Id).ToListAsync();
        events.Should().ContainSingle("the same print fetched across chunks/reruns persists once");
        events[0].Revisions.Should().HaveCount(2); // Scheduled + Released only
    }
}
