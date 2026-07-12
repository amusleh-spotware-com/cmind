extern alias mcp;
using Core.Calendar;
using Core.Options;
using FluentAssertions;
using Infrastructure.Calendar;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CalendarTools = mcp::Mcp.Tools.CalendarTools;

namespace IntegrationTests.Calendar;

public class CalendarToolsTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
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

    private static EconomicCalendarReader Reader(DataContext db)
    {
        var options = new StaticOptionsMonitor<AppOptions>(new AppOptions());
        var health = new CalendarHealthStore(db, options, new FixedTimeProvider(Now));
        return new EconomicCalendarReader(db, options, [], new NewsWindowPolicy(), health);
    }

    private static async Task SeedAsync(DataContext db)
    {
        var writer = new CalendarWriteService(db, new FixedTimeProvider(Now));
        var series = await writer.UpsertSeriesAsync(
            new SeriesCode("US.CPI.MoM"), new CountryCode("US"), "US CPI (MoM)",
            MarketMovingCategory.Inflation, ReleaseCadence.Monthly, 1.0, "FRED", "CPIAUCSL", CancellationToken.None);
        await writer.IngestReleaseAsync(series,
            new SourceReleaseItem("CPIAUCSL", Effective, Effective.AddMinutes(1), 3.1m, 2.9m, "%", "fred"),
            CancellationToken.None);
        await writer.IngestScheduleAsync(series,
            new SourceScheduleItem("CPIAUCSL", ReleaseWindow.Exact(Now.AddHours(2)), "UTC"), CancellationToken.None);
    }

    [Fact]
    public async Task CalendarEvents_tool_returns_seeded_event()
    {
        await using var db = await FreshAsync();
        await SeedAsync(db);
        var tools = new CalendarTools(Reader(db), new FixedTimeProvider(Now));

        var result = (IReadOnlyList<CalendarEventView>)await tools.CalendarEvents(
            from: "2024-01-01", to: "2025-01-01");

        result.Should().Contain(e => e.SeriesCode == "US.CPI.MOM" && e.Actual == 3.1m);
    }

    [Fact]
    public async Task CalendarBlackout_tool_flags_window()
    {
        await using var db = await FreshAsync();
        await SeedAsync(db);
        var tools = new CalendarTools(Reader(db), new FixedTimeProvider(Now));

        var result = (BlackoutResult)await tools.CalendarBlackout(
            "EURUSD", at: Now.AddHours(2).ToString("O"), minImpact: "Low", before: 30, after: 30);

        result.InBlackout.Should().BeTrue();
    }
}
