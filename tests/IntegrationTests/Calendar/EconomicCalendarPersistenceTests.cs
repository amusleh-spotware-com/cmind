using Core;
using Core.Calendar;
using Core.Options;
using FluentAssertions;
using Infrastructure.Calendar;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests.Calendar;

public class EconomicCalendarPersistenceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = new(2024, 3, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Effective = new(2024, 2, 13, 13, 30, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
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

    private static CalendarWriteService Writer(DataContext db) => new(db, new FixedTimeProvider(Now));

    private static EconomicCalendarReader Reader(DataContext db)
    {
        var options = new StaticOptionsMonitor<AppOptions>(new AppOptions());
        var health = new CalendarHealthStore(db, options, new FixedTimeProvider(Now));
        return new EconomicCalendarReader(db, options, [], new NewsWindowPolicy(), health);
    }

    private static async Task<EconomicSeries> SeedCpiAsync(DataContext db, double prior = 0.85)
    {
        return await Writer(db).UpsertSeriesAsync(
            new SeriesCode("US.CPI.MoM"), new CountryCode("US"), "US CPI (MoM)",
            MarketMovingCategory.Inflation, ReleaseCadence.Monthly, prior, "FRED", "CPIAUCSL",
            CancellationToken.None);
    }

    private static SourceReleaseItem Release(decimal actual, DateTimeOffset knownAt) =>
        new("CPIAUCSL", Effective, knownAt, actual, Previous: 2.9m, Unit: "%", SourceRef: "fred:2024-02-13");

    [Fact]
    public async Task Ingested_release_persists_and_reads_back()
    {
        await using var db = await FreshAsync();
        var series = await SeedCpiAsync(db);
        await Writer(db).IngestReleaseAsync(series, Release(3.1m, Effective.AddMinutes(1)), CancellationToken.None);

        var events = await Reader(db).GetEventsAsync(
            new CalendarQuery { From = Effective.AddDays(-1), To = Effective.AddDays(1) }, CancellationToken.None);

        events.Should().ContainSingle();
        events[0].Actual.Should().Be(3.1m);
        events[0].Released.Should().BeTrue();
        events[0].Impact.Should().Be(ImpactModel.Score(new ImpactInputs(0.85, 0, 0)).Level);
    }

    [Fact]
    public async Task Reingesting_the_same_release_is_idempotent()
    {
        await using var db = await FreshAsync();
        var series = await SeedCpiAsync(db);
        var writer = Writer(db);
        await writer.IngestReleaseAsync(series, Release(3.1m, Effective.AddMinutes(1)), CancellationToken.None);
        await writer.IngestReleaseAsync(series, Release(3.1m, Effective.AddMinutes(1)), CancellationToken.None);

        await using var verify = CreateContext();
        var persisted = await verify.EconomicEvents.SingleAsync();
        // Scheduled + Released only — the duplicate ingest appended nothing.
        persisted.Revisions.Should().HaveCount(2);
    }

    [Fact]
    public async Task Revision_appends_and_point_in_time_asof_excludes_look_ahead()
    {
        await using var db = await FreshAsync();
        var series = await SeedCpiAsync(db);
        var writer = Writer(db);
        var firstKnown = Effective.AddMinutes(1);
        var revisedKnown = Effective.AddDays(30);
        await writer.IngestReleaseAsync(series, Release(3.1m, firstKnown), CancellationToken.None);
        await writer.IngestReleaseAsync(series, Release(3.4m, revisedKnown), CancellationToken.None);

        var reader = Reader(db);
        var asOfFirst = await reader.GetEventsAsync(
            new CalendarQuery { From = Effective.AddDays(-1), To = Effective.AddDays(1), AsOf = Effective.AddDays(1) },
            CancellationToken.None);
        var asOfLatest = await reader.GetEventsAsync(
            new CalendarQuery { From = Effective.AddDays(-1), To = Effective.AddDays(1), AsOf = Effective.AddDays(60) },
            CancellationToken.None);

        asOfFirst[0].Actual.Should().Be(3.1m, "the revision was not yet known at that instant");
        asOfLatest[0].Actual.Should().Be(3.4m);
    }

    [Fact]
    public async Task Cursor_pagination_walks_events_without_overlap()
    {
        await using var db = await FreshAsync();
        var series = await SeedCpiAsync(db);
        var writer = Writer(db);
        foreach (var offset in new[] { 0, 1, 2 })
        {
            var effectiveAt = Effective.AddDays(offset);
            await writer.IngestReleaseAsync(series,
                new SourceReleaseItem("CPIAUCSL", effectiveAt, effectiveAt.AddMinutes(1), 3.1m, null, "%", "src"),
                CancellationToken.None);
        }

        var reader = Reader(db);
        var window = new CalendarQuery { From = Effective.AddDays(-1), To = Effective.AddDays(5), Limit = 2 };
        var page1 = await reader.GetEventsAsync(window, CancellationToken.None);
        page1.Should().HaveCount(2);
        page1.Select(e => e.EffectiveAt).Should().BeInAscendingOrder();

        var cursor = CalendarCursor.Encode(page1[^1].EffectiveAt, page1[^1].Id.Value);
        var page2 = await reader.GetEventsAsync(window with { Cursor = cursor }, CancellationToken.None);

        page2.Should().ContainSingle();
        page2[0].EffectiveAt.Should().Be(Effective.AddDays(2));
        page2.Select(e => e.Id).Should().NotIntersectWith(page1.Select(e => e.Id));
    }

    [Fact]
    public async Task For_symbol_returns_only_affecting_events_for_a_backtest_overlay()
    {
        await using var db = await FreshAsync();
        var writer = Writer(db);
        var us = await SeedCpiAsync(db);
        var jp = await writer.UpsertSeriesAsync(
            new SeriesCode("JP.GDP"), new CountryCode("JP"), "Japan GDP", MarketMovingCategory.Growth,
            ReleaseCadence.Quarterly, 0.8, "FRED", "JPNRGDPEXP", CancellationToken.None);
        await writer.IngestReleaseAsync(us, Release(3.1m, Effective.AddMinutes(1)), CancellationToken.None);
        await writer.IngestReleaseAsync(jp,
            new SourceReleaseItem("JPNRGDPEXP", Effective, Effective.AddMinutes(1), 1.0m, null, null, "src"),
            CancellationToken.None);

        var reader = Reader(db);
        var window = (Effective.AddDays(-1), Effective.AddDays(1));

        // EURUSD is exposed to USD (US) but not JPY (JP) → only the US event overlays.
        var forEur = await reader.GetEventsForSymbolAsync(
            new Symbol("EURUSD"), window.Item1, window.Item2, null, CancellationToken.None);
        forEur.Select(e => e.SeriesCode).Should().BeEquivalentTo(["US.CPI.MOM"]);

        // USDJPY is exposed to both.
        var forJpy = await reader.GetEventsForSymbolAsync(
            new Symbol("USDJPY"), window.Item1, window.Item2, null, CancellationToken.None);
        forJpy.Select(e => e.SeriesCode).Should().BeEquivalentTo(["US.CPI.MOM", "JP.GDP"]);
    }

    [Fact]
    public async Task Blackout_reader_reports_symbol_in_window()
    {
        await using var db = await FreshAsync();
        var series = await SeedCpiAsync(db, prior: 1.0);
        // A scheduled future event, known as of Now.
        var future = Now.AddHours(2);
        await Writer(db).IngestScheduleAsync(
            series, new SourceScheduleItem("CPIAUCSL", ReleaseWindow.Exact(future), "UTC"), CancellationToken.None);

        var rule = new NewsWindowRule(ImpactLevel.Low, 30, 30);
        var result = await Reader(db).GetBlackoutAsync(new Symbol("EURUSD"), future, rule, CancellationToken.None);

        result.InBlackout.Should().BeTrue();
        result.Trigger!.Value.Country.Value.Should().Be("US");
    }

    [Fact]
    public async Task Surprises_expose_the_series_history()
    {
        await using var db = await FreshAsync();
        var series = await SeedCpiAsync(db);
        var writer = Writer(db);
        await writer.IngestReleaseAsync(series, Release(3.1m, Effective.AddMinutes(1)), CancellationToken.None);

        var points = await Reader(db).GetSurprisesAsync(new SeriesCode("US.CPI.MoM"), 10, null, CancellationToken.None);

        points.Should().ContainSingle();
        points[0].Actual.Should().Be(3.1m);
    }
}
