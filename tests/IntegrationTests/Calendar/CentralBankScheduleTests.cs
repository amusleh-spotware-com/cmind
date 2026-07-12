using Core.Calendar;
using Core.Options;
using FluentAssertions;
using Infrastructure.Calendar;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests.Calendar;

public class CentralBankScheduleTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = new(2024, 12, 1, 0, 0, 0, TimeSpan.Zero);

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

    [Fact]
    public async Task Scheduling_fomc_meetings_creates_unreleased_future_events()
    {
        await using var db = await FreshAsync();
        var writer = new CalendarWriteService(db, new FixedTimeProvider(Now));
        var source = new CentralBankScheduleSource();

        var series = await writer.UpsertSeriesAsync(
            new SeriesCode("US.FOMC"), new CountryCode("US"), "US Fed Funds Rate Decision (FOMC)",
            MarketMovingCategory.InterestRate, ReleaseCadence.PerMeeting, 0.98, "CentralBankSchedule", "FOMC",
            CancellationToken.None);

        var from = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero);
        foreach (var item in await source.FetchScheduleAsync("FOMC", from, to, CancellationToken.None))
            await writer.IngestScheduleAsync(series, item, CancellationToken.None);

        // Idempotent: re-syncing the same schedule adds nothing.
        foreach (var item in await source.FetchScheduleAsync("FOMC", from, to, CancellationToken.None))
            await writer.IngestScheduleAsync(series, item, CancellationToken.None);

        var reader = new EconomicCalendarReader(
            db, new StaticOptionsMonitor<AppOptions>(new AppOptions()), [], new NewsWindowPolicy(),
            new CalendarHealthStore(db, new StaticOptionsMonitor<AppOptions>(new AppOptions()), new FixedTimeProvider(Now)));

        var events = await reader.GetEventsAsync(
            new CalendarQuery { From = from, To = to, Limit = 100 }, CancellationToken.None);

        events.Should().HaveCount(8);
        events.Should().OnlyContain(e => !e.Released && e.SeriesCode == "US.FOMC");
    }
}
