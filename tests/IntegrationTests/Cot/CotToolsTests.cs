extern alias mcp;
using Core.Cot;
using Core.Options;
using FluentAssertions;
using Infrastructure.Cot;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CotTools = mcp::Mcp.Tools.CotTools;

namespace IntegrationTests.Cot;

public class CotToolsTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = new(2024, 3, 1, 0, 0, 0, TimeSpan.Zero);
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

    private static CotReader Reader(DataContext db)
    {
        var options = new StaticOptionsMonitor<AppOptions>(new AppOptions());
        var health = new CotHealthStore(db, options, new FixedTimeProvider(Now));
        return new CotReader(db, options, health);
    }

    private static async Task SeedAsync(DataContext db, int weeks = 3)
    {
        var writer = new CotWriteService(db);
        for (var i = 0; i < weeks; i++)
        {
            var reportDate = new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero).AddDays(7 * i);
            await writer.IngestAsync(new CotSourceReport(
                Code, "Euro FX", "CHICAGO MERCANTILE EXCHANGE", CotReportKind.Legacy, false,
                reportDate, 700000, -1500,
                [
                    new CotSourceCategory(CotTraderCategory.NonCommercial, 200000 + 10000 * i, 120000, 30000, null, null),
                    new CotSourceCategory(CotTraderCategory.Commercial, 300000, 350000, 0, null, null),
                    new CotSourceCategory(CotTraderCategory.NonReportable, 40000, 50000, 0, null, null)
                ]), CancellationToken.None);
        }
    }

    [Fact]
    public async Task CotLatest_tool_returns_seeded_report_with_index()
    {
        await using var db = await FreshAsync();
        await SeedAsync(db);
        var tools = new CotTools(Reader(db), new FixedTimeProvider(Now));

        var view = (CotReportView?)await tools.CotLatest(Code, "Legacy");

        view.Should().NotBeNull();
        view!.ContractCode.Should().Be(Code);
        view.Categories.Should().HaveCount(3);
        view.CotIndex.Should().NotBeNull();
    }

    [Fact]
    public async Task CotHistory_tool_returns_points()
    {
        await using var db = await FreshAsync();
        await SeedAsync(db);
        var tools = new CotTools(Reader(db), new FixedTimeProvider(Now));

        var history = (IReadOnlyList<CotHistoryPoint>)await tools.CotHistory(Code, "Legacy");

        history.Should().HaveCountGreaterThan(0);
        history[^1].SpeculatorNet.Should().Be(220000 - 120000);
    }

    [Fact]
    public async Task CotMarkets_tool_lists_the_market()
    {
        await using var db = await FreshAsync();
        await SeedAsync(db);
        var tools = new CotTools(Reader(db), new FixedTimeProvider(Now));

        var markets = (IReadOnlyList<CotMarketView>)await tools.CotMarkets();

        markets.Should().Contain(m => m.ContractCode == Code);
    }

    [Fact]
    public async Task GetReportAsync_window_includes_the_report_itself_so_index_is_computed()
    {
        await using var db = await FreshAsync();
        await SeedAsync(db);
        var reads = Reader(db);

        var latest = await reads.GetLatestAsync(
            new ContractMarketCode(Code), CotReportKind.Legacy, false, null, CancellationToken.None);
        latest.Should().NotBeNull();

        // Loading the same report by id must compute the COT index over a window that INCLUDES this report
        // (regression: it was filtered out because its Friday KnownAt post-dates its Tuesday ReportDate).
        var byId = await reads.GetReportAsync(latest!.Id, null, CancellationToken.None);
        byId.Should().NotBeNull();
        byId!.CotIndex.Should().Be(latest.CotIndex);
        byId.Categories.Should().HaveCount(3);
    }

    [Fact]
    public async Task CotLatest_honours_point_in_time_asOf()
    {
        await using var db = await FreshAsync();
        await SeedAsync(db);
        var tools = new CotTools(Reader(db), new FixedTimeProvider(Now));

        // Before the first report's Friday release, nothing is public yet.
        var early = (CotReportView?)await tools.CotLatest(Code, "Legacy", false, "2024-01-01T00:00:00Z");
        early.Should().BeNull();
    }
}
