using Core;
using Core.CopyTrading;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Nodes.CopyTrading;
using Testcontainers.PostgreSql;
using Xunit;

namespace IntegrationTests.CopyLive;

// Phase 3 execution transparency against a real Postgres: the drainer takes the copy host's execution
// facts from the channel sink and persists them to the CopyExecution append-only log, which the
// transparency read model queries. Proves the out-of-band host -> DB flow the report depends on.
public sealed class CopyExecutionDrainerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().WithImage("postgres:17").Build();
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _connectionString = _postgres.GetConnectionString();
        await using var db = NewContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private DataContext NewContext()
        => new(new DbContextOptionsBuilder<DataContext>().UseNpgsql(_connectionString)
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System)).Options);

    [Fact]
    public async Task Drainer_persists_buffered_execution_facts_to_the_log()
    {
        var services = new ServiceCollection();
        services.AddDbContext<DataContext>(o => o
            .UseNpgsql(_connectionString)
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System)));
        await using var provider = services.BuildServiceProvider();

        var sink = new ChannelCopyEventSink();
        var drainer = new CopyExecutionDrainer(sink, provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CopyExecutionDrainer>.Instance, TimeProvider.System);

        var profileId = CopyProfileId.New();
        for (var i = 0; i < 3; i++)
            sink.Record(new CopyExecutionRecord(profileId, DestinationCtidTraderAccountId: 200 + i,
                SourcePositionId: 7000 + i, Symbol: "EURUSD", Kind: CopyExecutionKind.Opened, IsBuy: i % 2 == 0,
                Volume: 100, MasterPrice: 1.10, SlippagePoints: 2, LatencyMilliseconds: 5, Reason: null,
                OccurredAt: TestClock.Now));

        await drainer.DrainOnceAsync(default);

        await using var db = NewContext();
        var rows = await db.CopyExecutions.Where(x => x.ProfileId == profileId.Value)
            .OrderBy(x => x.SourcePositionId).ToListAsync();
        rows.Should().HaveCount(3, "every buffered fact is persisted");
        rows.Should().OnlyContain(x => x.Kind == CopyExecutionKind.Opened && x.Symbol == "EURUSD");
        rows[0].SourcePositionId.Should().Be(7000);
    }

    [Fact]
    public async Task Draining_an_empty_sink_writes_nothing()
    {
        var services = new ServiceCollection();
        services.AddDbContext<DataContext>(o => o
            .UseNpgsql(_connectionString)
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System)));
        await using var provider = services.BuildServiceProvider();

        var drainer = new CopyExecutionDrainer(new ChannelCopyEventSink(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CopyExecutionDrainer>.Instance, TimeProvider.System);

        await drainer.DrainOnceAsync(default);

        await using var db = NewContext();
        (await db.CopyExecutions.CountAsync()).Should().Be(0);
    }
}
