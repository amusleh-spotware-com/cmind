using Core;
using Core.Domain;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Nodes.CopyTrading;
using Testcontainers.PostgreSql;
using Xunit;

namespace IntegrationTests.CopyLive;

// Node affinity against a real Postgres: proves the atomic claim the supervisor uses so two
// co-located supervisors never host (double-copy) the same running profile, and that stop/pause
// releases the claim for another node to pick up.
public sealed class CopyNodeAffinityTests : IAsyncLifetime
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
        => new(new DbContextOptionsBuilder<DataContext>().UseNpgsql(_connectionString).Options);

    private async Task<UserId> SeedUserAsync(DataContext db)
    {
        var user = RegularUser.Create(new Email($"u{Guid.NewGuid():N}@example.com"), "hash", new byte[] { 1, 2, 3 });
        db.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static CopyProfile RunningProfile(UserId userId, string name)
    {
        var profile = CopyProfile.Create(userId, name, TradingAccountId.New());
        profile.Start();
        return profile;
    }

    [Fact]
    public async Task Only_one_node_claims_each_running_profile()
    {
        await using var db = NewContext();
        var userId = await SeedUserAsync(db);
        var ids = new List<CopyProfileId>();
        foreach (var i in Enumerable.Range(0, 3))
        {
            var profile = RunningProfile(userId, $"p{i}-{Guid.NewGuid():N}");
            db.Add(profile);
            ids.Add(profile.Id);
        }
        await db.SaveChangesAsync();

        var nodeA = new NodeIdentity("node-a");
        var nodeB = new NodeIdentity("node-b");

        var now = DateTimeOffset.UtcNow;
        var ttl = TimeSpan.FromMinutes(2);
        var claimedByA = await CopyEngineSupervisor.ClaimProfilesAsync(db, nodeA, now, ttl, default);
        var claimedByB = await CopyEngineSupervisor.ClaimProfilesAsync(db, nodeB, now, ttl, default);

        claimedByA.Should().Be(3, "the first node to run the claim owns every unassigned running profile");
        claimedByB.Should().Be(0, "nothing is left for the second node — no double-hosting");

        db.ChangeTracker.Clear(); // ExecuteUpdate bypasses the tracker — read the persisted state fresh
        var profiles = await db.CopyProfiles.Where(p => ids.Contains(p.Id)).ToListAsync();
        profiles.Should().OnlyContain(p => p.IsHostedBy(nodeA));
        profiles.Should().NotContain(p => p.IsHostedBy(nodeB));
    }

    [Fact]
    public async Task Pausing_releases_the_claim_so_another_node_can_host()
    {
        await using var db = NewContext();
        var userId = await SeedUserAsync(db);
        var profile = RunningProfile(userId, $"p-{Guid.NewGuid():N}");
        db.Add(profile);
        await db.SaveChangesAsync();

        var nodeA = new NodeIdentity("node-a");
        var nodeB = new NodeIdentity("node-b");

        var now = DateTimeOffset.UtcNow;
        var ttl = TimeSpan.FromMinutes(2);
        await CopyEngineSupervisor.ClaimProfilesAsync(db, nodeA, now, ttl, default);
        db.ChangeTracker.Clear();
        var owned = await db.CopyProfiles.FirstAsync(p => p.Id == profile.Id);
        owned.IsHostedBy(nodeA).Should().BeTrue();

        // Pause (releases) then restart → the claim is free again.
        owned.Pause();
        owned.Start();
        await db.SaveChangesAsync();

        var reclaimed = await CopyEngineSupervisor.ClaimProfilesAsync(db, nodeB, now, ttl, default);
        reclaimed.Should().Be(1, "a released profile can be claimed by a different node");
        db.ChangeTracker.Clear();
        (await db.CopyProfiles.FirstAsync(p => p.Id == profile.Id)).IsHostedBy(nodeB).Should().BeTrue();
    }

    [Fact]
    public async Task Expired_lease_is_reclaimed_by_another_node()
    {
        await using var db = NewContext();
        var userId = await SeedUserAsync(db);
        var profile = RunningProfile(userId, $"p-{Guid.NewGuid():N}");
        db.Add(profile);
        await db.SaveChangesAsync();

        var nodeA = new NodeIdentity("node-a");
        var nodeB = new NodeIdentity("node-b");

        // node-a claims with a lease that is already in the past (simulates a node that then dies).
        var claimedByA = await CopyEngineSupervisor.ClaimProfilesAsync(
            db, nodeA, DateTimeOffset.UtcNow.AddMinutes(-5), TimeSpan.FromSeconds(1), default);
        claimedByA.Should().Be(1);
        db.ChangeTracker.Clear();

        // A live node-b sees the lapsed lease and reclaims the profile — copying self-heals.
        var claimedByB = await CopyEngineSupervisor.ClaimProfilesAsync(
            db, nodeB, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), default);
        claimedByB.Should().Be(1, "an expired lease lets another node take over a dead node's profile");
        db.ChangeTracker.Clear();
        (await db.CopyProfiles.FirstAsync(p => p.Id == profile.Id)).IsHostedBy(nodeB).Should().BeTrue();
    }
}
