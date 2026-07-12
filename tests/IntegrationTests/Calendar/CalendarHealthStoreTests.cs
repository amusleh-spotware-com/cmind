using Core.Options;
using FluentAssertions;
using Infrastructure.Calendar;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests.Calendar;

public class CalendarHealthStoreTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = new(2024, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static readonly AppOptions Options = new()
    {
        Calendar = new CalendarOptions { SourceStaleAfter = TimeSpan.FromHours(1), CircuitFailureThreshold = 3 }
    };

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
        await db.AppSettings.Where(s => s.Key.StartsWith("calendar.source.")).ExecuteDeleteAsync();
        return db;
    }

    private static CalendarHealthStore Store(DataContext db, DateTimeOffset at) =>
        new(db, new StaticOptionsMonitor<AppOptions>(Options), new FixedTimeProvider(at));

    [Fact]
    public async Task An_unpolled_source_reports_stale_with_no_last_success()
    {
        await using var db = await FreshAsync();

        var health = await Store(db, Now).GetAllAsync(["FRED"], CancellationToken.None);

        health.Should().ContainSingle();
        health[0].SourceName.Should().Be("FRED");
        health[0].Stale.Should().BeTrue();
        health[0].LastSuccessfulPollAt.Should().BeNull();
    }

    [Fact]
    public async Task Recording_success_clears_staleness_until_the_window_lapses()
    {
        await using var db = await FreshAsync();
        await Store(db, Now).RecordSuccessAsync("FRED", CancellationToken.None);

        var fresh = await Store(db, Now.AddMinutes(30)).GetAllAsync(["FRED"], CancellationToken.None);
        fresh[0].Stale.Should().BeFalse();
        fresh[0].LastSuccessfulPollAt.Should().Be(Now);

        // Once the staleness window (1h) lapses, the same recorded success is reported stale again.
        var later = await Store(db, Now.AddHours(2)).GetAllAsync(["FRED"], CancellationToken.None);
        later[0].Stale.Should().BeTrue();
    }

    [Fact]
    public async Task Consecutive_failures_trip_the_circuit_open_flag()
    {
        await using var db = await FreshAsync();
        var store = Store(db, Now);

        await store.RecordFailureAsync("FRED", CancellationToken.None);
        await store.RecordFailureAsync("FRED", CancellationToken.None);
        (await store.GetAllAsync(["FRED"], CancellationToken.None))[0].CircuitOpen.Should().BeFalse();

        await store.RecordFailureAsync("FRED", CancellationToken.None); // hits threshold (3)
        (await store.GetAllAsync(["FRED"], CancellationToken.None))[0].CircuitOpen.Should().BeTrue();

        // A success resets the circuit.
        await store.RecordSuccessAsync("FRED", CancellationToken.None);
        (await store.GetAllAsync(["FRED"], CancellationToken.None))[0].CircuitOpen.Should().BeFalse();
    }
}
