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
