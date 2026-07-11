using Core;
using Core.CopyTrading;
using Core.Domain;
using Core.Options;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nodes.CopyTrading;
using Testcontainers.PostgreSql;
using Xunit;

// Phase 4 performance-fee settlement against a real Postgres: the service polls each fee-configured
// destination's equity (via a fake reader here), settles the high-water-mark fee on the aggregate, persists
// the advanced mark, and records a CopyFeeAccrual only on a new high.
public sealed class CopyFeeSettlementTests : IAsyncLifetime
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

    private sealed class FakeEquityReader(double equity) : ICopyEquityReader
    {
        public Task<double?> ReadEquityAsync(long destinationCtidTraderAccountId, CancellationToken ct)
            => Task.FromResult<double?>(equity);
    }

    private static CopyFeeSettlementService Service(IServiceProvider provider, double equity)
        => new(provider.GetRequiredService<IServiceScopeFactory>(), new FakeEquityReader(equity),
            provider.GetRequiredService<IOptionsMonitor<AppOptions>>(),
            NullLogger<CopyFeeSettlementService>.Instance, TimeProvider.System);

    [Fact]
    public async Task Settlement_seeds_the_mark_then_accrues_a_fee_on_a_new_high()
    {
        CopyProfileId profileId;
        CopyDestinationId destinationId;
        UserId ownerId;
        await using (var seed = NewContext())
        {
            var user = RegularUser.Create(new Email($"u{Guid.NewGuid():N}@example.com"), "hash", new byte[] { 1, 2, 3 });
            seed.Add(user);
            var cid = CTraderIdAccount.Create(user.Id, "u", new byte[] { 1 });
            var account = cid.LinkOpenApiAccount(111, "Broker", isLive: false,
                new CtidTraderAccountId(555), OpenApiAuthorizationId.New(), null);
            seed.Add(cid);

            var profile = CopyProfile.Create(user.Id, $"p-{Guid.NewGuid():N}", TradingAccountId.New());
            var destination = profile.AddDestination(account.Id, RiskSettings.Default);
            destination.SetPerformanceFee(new PerformanceFee(20));
            profile.Start();
            // The settlement service only settles profiles this node hosts, so assign it to this node.
            profile.AssignToNode(new NodeIdentity(Environment.MachineName));
            seed.Add(profile);
            await seed.SaveChangesAsync();

            profileId = profile.Id;
            destinationId = destination.Id;
            ownerId = user.Id;
        }

        var services = new ServiceCollection();
        services.AddDbContext<DataContext>(o => o
            .UseNpgsql(_connectionString).AddInterceptors(new AuditStampingInterceptor(TimeProvider.System)));
        services.AddOptions<AppOptions>();
        await using var provider = services.BuildServiceProvider();

        // First settlement seeds the high-water-mark at 10,000 and charges nothing.
        await Service(provider, 10_000).SettleAsync(default);
        await using (var check = NewContext())
        {
            (await check.CopyFeeAccruals.CountAsync()).Should().Be(0, "the opening balance is not charged");
            var dest = await check.CopyProfiles.Include(p => p.Destinations)
                .SelectMany(p => p.Destinations).FirstAsync(d => d.Id == destinationId);
            dest.HighWaterMarkEquity.Should().Be(10_000);
        }

        // Second settlement at a new high of 12,000 charges 20% of the 2,000 gain.
        await Service(provider, 12_000).SettleAsync(default);
        await using (var check = NewContext())
        {
            var accrual = await check.CopyFeeAccruals.SingleAsync();
            accrual.FeeAmount.Should().Be(400);
            accrual.ProfileId.Should().Be(profileId.Value);
            accrual.DestinationId.Should().Be(destinationId.Value);
            accrual.UserId.Should().Be(ownerId);
            var dest = await check.CopyProfiles.SelectMany(p => p.Destinations).FirstAsync(d => d.Id == destinationId);
            dest.HighWaterMarkEquity.Should().Be(12_000, "the mark advances to the new high");
        }

        // A settlement below the peak accrues nothing and leaves the mark untouched.
        await Service(provider, 11_000).SettleAsync(default);
        await using (var check = NewContext())
        {
            (await check.CopyFeeAccruals.CountAsync()).Should().Be(1, "below the peak — no new fee");
            var dest = await check.CopyProfiles.SelectMany(p => p.Destinations).FirstAsync(d => d.Id == destinationId);
            dest.HighWaterMarkEquity.Should().Be(12_000);
        }
    }
}
