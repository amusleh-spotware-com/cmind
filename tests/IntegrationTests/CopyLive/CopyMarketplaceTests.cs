using Core;
using Core.CopyTrading;
using Core.Domain;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Web.Endpoints;
using Xunit;

namespace IntegrationTests.CopyLive;

// Phase 4 marketplace against a real Postgres: a provider listing persists with its verified-live badge,
// only one listing may exist per profile (unique index), and the ranking score orders providers as
// expected (verified + high fill beats unverified + low fill).
public sealed class CopyMarketplaceTests : IAsyncLifetime
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
    public async Task A_published_listing_persists_with_its_verified_live_badge()
    {
        var profileId = CopyProfileId.New();
        await using (var seed = NewContext())
        {
            var user = RegularUser.Create(new Email($"u{Guid.NewGuid():N}@example.com"), "hash", new byte[] { 1 });
            seed.Add(user);
            var listing = CopyProviderListing.Create(user.Id, profileId, "Alpha", "desc", new PerformanceFee(20), verifiedLive: true);
            listing.Publish(TestClock.Now);
            seed.Add(listing);
            await seed.SaveChangesAsync();
        }

        await using var db = NewContext();
        var stored = await db.CopyProviderListings.SingleAsync(l => l.ProfileId == profileId);
        stored.Published.Should().BeTrue();
        stored.VerifiedLive.Should().BeTrue();
        stored.PublishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Only_one_listing_may_exist_per_profile()
    {
        var profileId = CopyProfileId.New();
        UserId userId;
        await using (var seed = NewContext())
        {
            var user = RegularUser.Create(new Email($"u{Guid.NewGuid():N}@example.com"), "hash", new byte[] { 1 });
            seed.Add(user);
            userId = user.Id;
            seed.Add(CopyProviderListing.Create(user.Id, profileId, "Alpha", null, PerformanceFee.None, false));
            await seed.SaveChangesAsync();
        }

        await using var db = NewContext();
        db.Add(CopyProviderListing.Create(userId, profileId, "Beta", null, PerformanceFee.None, false));
        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("the unique index forbids a second listing for the profile");
    }

    [Fact]
    public async Task Marketplace_stats_are_aggregated_in_the_database()
    {
        var profileId = CopyProfileId.New();
        await using (var seed = NewContext())
        {
            void Add(CopyExecutionKind kind, double latency, int? slippage) =>
                seed.CopyExecutions.Add(CopyExecution.From(new CopyExecutionRecord(profileId, 200, 7000, "EURUSD",
                    kind, IsBuy: true, Volume: 100, MasterPrice: 1.10, SlippagePoints: slippage,
                    LatencyMilliseconds: latency, Reason: null, OccurredAt: TestClock.Now)));
            Add(CopyExecutionKind.Opened, 100, 2);
            Add(CopyExecutionKind.Opened, 200, null);
            Add(CopyExecutionKind.Failed, 0, null);
            await seed.SaveChangesAsync();
        }

        await using var db = NewContext();
        // Runs the real GroupBy aggregation against Postgres — proves it translates (no full materialization).
        var stats = (await CopyEndpoints.LoadMarketplaceStatsAsync(db, [profileId.Value], default)).Single();
        stats.Total.Should().Be(3);
        stats.Opened.Should().Be(2);
        stats.Failed.Should().Be(1);
        stats.LatencySum.Should().Be(300, "sum of the two opened copies' latency (100 + 200)");
        stats.SlippageSum.Should().Be(2);
        stats.SlippageCount.Should().Be(1, "only one execution carried a slippage value");
    }

    [Fact]
    public void The_ranking_score_prefers_verified_high_fill_providers()
    {
        var strong = CopyEndpoints.MarketplaceScore(fillRate: 0.98, avgLatencyMs: 100, avgSlippagePoints: 1, verifiedLive: true);
        var weak = CopyEndpoints.MarketplaceScore(fillRate: 0.50, avgLatencyMs: 1500, avgSlippagePoints: 8, verifiedLive: false);

        strong.Should().BeGreaterThan(weak);
        strong.Should().BeInRange(0, 100);
    }
}
