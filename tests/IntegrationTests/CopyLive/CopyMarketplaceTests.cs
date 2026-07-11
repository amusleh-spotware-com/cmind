using Core;
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
            var listing = CopyProviderListing.Create(user.Id, profileId, "Alpha", "desc", 20, verifiedLive: true);
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
            seed.Add(CopyProviderListing.Create(user.Id, profileId, "Alpha", null, 0, false));
            await seed.SaveChangesAsync();
        }

        await using var db = NewContext();
        db.Add(CopyProviderListing.Create(userId, profileId, "Beta", null, 0, false));
        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("the unique index forbids a second listing for the profile");
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
