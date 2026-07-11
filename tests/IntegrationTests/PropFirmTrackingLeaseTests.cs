using Core;
using Core.Domain;
using Core.PropFirm;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Nodes.PropFirm;
using Xunit;

namespace IntegrationTests;

public class PropFirmTrackingLeaseTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System))
            .Options);

    [Fact]
    public async Task Active_challenge_is_claimed_then_reclaimed_after_lease_lapses()
    {
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();

        var user = OwnerUser.Create(new Email($"lease-{Guid.NewGuid():N}@test.local"), "x",
            Guid.NewGuid().ToByteArray());
        var challenge = PropFirmChallenge.Create(user.Id, TradingAccountId.New(), "Lease 100k",
            new Money(100_000m),
            new ChallengeRules(new Percent(10), new Percent(5), new Percent(10),
                DrawdownMode.Static, new TradingDayRequirement(0), SingleStep: true));

        await using (var write = CreateContext())
        {
            write.Users.Add(user);
            write.PropFirmChallenges.Add(challenge);
            await write.SaveChangesAsync();
        }

        var nodeA = new NodeIdentity("node-a");
        var nodeB = new NodeIdentity("node-b");
        var start = new DateTimeOffset(2026, 07, 10, 12, 0, 0, TimeSpan.Zero);
        var leaseTtl = TimeSpan.FromSeconds(120);

        await using (var first = CreateContext())
        {
            var claimed = await PropFirmTrackingSupervisor.ClaimChallengesAsync(first, nodeA, start, leaseTtl, default);
            claimed.Should().Be(1);
        }

        // node-b cannot steal a live lease.
        await using (var contested = CreateContext())
        {
            var stolen = await PropFirmTrackingSupervisor.ClaimChallengesAsync(
                contested, nodeB, start.AddSeconds(30), leaseTtl, default);
            stolen.Should().Be(0);
        }

        // After the lease lapses (node-a died), node-b reclaims it (self-heal).
        await using (var reclaim = CreateContext())
        {
            var reclaimed = await PropFirmTrackingSupervisor.ClaimChallengesAsync(
                reclaim, nodeB, start + leaseTtl + TimeSpan.FromSeconds(1), leaseTtl, default);
            reclaimed.Should().Be(1);
        }

        await using var read = CreateContext();
        var loaded = await read.PropFirmChallenges.FirstAsync(c => c.Id == challenge.Id);
        loaded.AssignedNode.Should().Be("node-b");
    }
}
