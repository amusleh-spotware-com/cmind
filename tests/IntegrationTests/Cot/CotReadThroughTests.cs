using Core.Cot;
using Core.Options;
using FluentAssertions;
using Infrastructure.Cot;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests.Cot;

public class CotReadThroughTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = new(2024, 1, 20, 0, 0, 0, TimeSpan.Zero);
    private const string Code = "099741";

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
        await db.Database.ExecuteSqlRawAsync("TRUNCATE cot.category_position, cot.report, cot.market CASCADE");
        return db;
    }

    private static IReadOnlyList<CotSourceReport> ThreeWeeks()
    {
        var reports = new List<CotSourceReport>();
        for (var i = 0; i < 3; i++)
        {
            var reportDate = new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero).AddDays(7 * i);
            reports.Add(new CotSourceReport(
                Code, "Euro FX", "CME", CotReportKind.Legacy, false, reportDate, 700000, -1500,
                [
                    new CotSourceCategory(CotTraderCategory.NonCommercial, 200000 + 10000 * i, 120000, 30000, null, null),
                    new CotSourceCategory(CotTraderCategory.Commercial, 300000, 350000, 0, null, null),
                    new CotSourceCategory(CotTraderCategory.NonReportable, 40000, 50000, 0, null, null)
                ]));
        }

        return reports;
    }

    private static CotReadThroughReports Decorator(DataContext db, FakeCotSource source, out CotLoadGate gate)
    {
        var options = new StaticOptionsMonitor<AppOptions>(new AppOptions());
        var time = new FixedTimeProvider(Now);
        var health = new CotHealthStore(db, options, time);
        var reader = new CotReader(db, options, health);
        var writer = new CotWriteService(db);
        gate = new CotLoadGate();
        return new CotReadThroughReports(reader, source, writer, health, db, gate, options, time);
    }

    [Fact]
    public async Task First_request_loads_from_source_and_caches_in_db()
    {
        await using var db = await FreshAsync();
        var source = new FakeCotSource { Provider = (_, _) => ThreeWeeks() };
        var reads = Decorator(db, source, out _);

        var latest = await reads.GetLatestAsync(
            new ContractMarketCode(Code), CotReportKind.Legacy, false, null, CancellationToken.None);

        latest.Should().NotBeNull();
        latest!.Categories.Should().HaveCount(3);
        source.CallCount.Should().Be(1, "the first request loads the market from the source");
        (await db.CotReports.CountAsync()).Should().Be(3, "the fetched reports are cached in the database");
    }

    [Fact]
    public async Task Subsequent_request_is_served_from_db_without_refetching()
    {
        await using var db = await FreshAsync();
        var source = new FakeCotSource { Provider = (_, _) => ThreeWeeks() };
        var reads = Decorator(db, source, out _);

        await reads.GetLatestAsync(new ContractMarketCode(Code), CotReportKind.Legacy, false, null, CancellationToken.None);
        var again = await reads.GetHistoryAsync(
            new ContractMarketCode(Code), CotReportKind.Legacy, false, Now.AddYears(-1), Now, null, CancellationToken.None);

        again.Should().NotBeEmpty();
        source.CallCount.Should().Be(1, "once cached and fresh, subsequent requests are served from the database only");
    }

    [Fact]
    public async Task Market_with_no_source_data_is_not_refetched_on_every_request()
    {
        await using var db = await FreshAsync();
        var source = new FakeCotSource(); // returns nothing
        var reads = Decorator(db, source, out _);
        var code = new ContractMarketCode(Code);

        var first = await reads.GetLatestAsync(code, CotReportKind.Legacy, false, null, CancellationToken.None);
        var second = await reads.GetLatestAsync(code, CotReportKind.Legacy, false, null, CancellationToken.None);

        first.Should().BeNull();
        second.Should().BeNull();
        source.CallCount.Should().Be(1, "an empty market is attempted once then throttled, not fetched every request");
    }

    [Fact]
    public async Task Markets_are_seeded_on_first_catalog_request()
    {
        await using var db = await FreshAsync();
        var source = new FakeCotSource();
        var reads = Decorator(db, source, out _);

        var markets = await reads.GetMarketsAsync(null, null, CancellationToken.None);

        markets.Should().Contain(m => m.ContractCode == Code, "the curated catalog is seeded on demand");
        source.CallCount.Should().Be(0, "listing the catalog does not fetch report data");
    }
}
