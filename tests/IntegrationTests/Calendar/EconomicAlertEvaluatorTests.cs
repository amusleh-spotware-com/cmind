using Core;
using Core.Calendar;
using Core.Domain;
using Core.Options;
using FluentAssertions;
using Infrastructure.Calendar;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Nodes.Alerts;
using Xunit;

namespace IntegrationTests.Calendar;

public class EconomicAlertEvaluatorTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = new(2024, 12, 1, 12, 0, 0, TimeSpan.Zero);

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
        return new EconomicCalendarReader(db, options, [], new NewsWindowPolicy(),
            new CalendarHealthStore(db, options, new FixedTimeProvider(Now)));
    }

    [Fact]
    public async Task Raises_for_an_upcoming_event_in_the_window_then_dedups()
    {
        await using var db = await FreshAsync();
        var writer = new CalendarWriteService(db, new FixedTimeProvider(Now));
        var series = await writer.UpsertSeriesAsync(
            new SeriesCode("US.CPI"), new CountryCode("US"), "US CPI", MarketMovingCategory.Inflation,
            ReleaseCadence.Monthly, 0.9, "FRED", "CPIAUCSL", CancellationToken.None);
        await writer.IngestScheduleAsync(series,
            new SourceScheduleItem("CPIAUCSL", ReleaseWindow.Exact(Now.AddMinutes(30)), "UTC"), CancellationToken.None);

        var evaluator = new EconomicAlertEvaluator(Reader(db), new FixedTimeProvider(Now));
        var rule = AlertRule.CreateEconomicEvent(
            UserId.New(), "CPI watch", ImpactLevel.Low, minutesBefore: 60, currencies: null,
            new EvaluationInterval(15));

        var first = await evaluator.EvaluateAsync(rule, CancellationToken.None);
        first.Should().NotBeNull();
        rule.Events.Should().ContainSingle();

        // Same upcoming event on the next cycle — deduplicated, no new alert.
        var second = await evaluator.EvaluateAsync(rule, CancellationToken.None);
        second.Should().BeNull();
        rule.Events.Should().ContainSingle();
    }

    [Fact]
    public async Task Does_not_raise_when_nothing_matches_the_window()
    {
        await using var db = await FreshAsync();
        var writer = new CalendarWriteService(db, new FixedTimeProvider(Now));
        var series = await writer.UpsertSeriesAsync(
            new SeriesCode("US.CPI"), new CountryCode("US"), "US CPI", MarketMovingCategory.Inflation,
            ReleaseCadence.Monthly, 0.9, "FRED", "CPIAUCSL", CancellationToken.None);
        // Event far outside the 60-minute lead window.
        await writer.IngestScheduleAsync(series,
            new SourceScheduleItem("CPIAUCSL", ReleaseWindow.Exact(Now.AddDays(5)), "UTC"), CancellationToken.None);

        var evaluator = new EconomicAlertEvaluator(Reader(db), new FixedTimeProvider(Now));
        var rule = AlertRule.CreateEconomicEvent(
            UserId.New(), "CPI watch", ImpactLevel.Low, minutesBefore: 60, currencies: null,
            new EvaluationInterval(15));

        (await evaluator.EvaluateAsync(rule, CancellationToken.None)).Should().BeNull();
        rule.Events.Should().BeEmpty();
    }
}
