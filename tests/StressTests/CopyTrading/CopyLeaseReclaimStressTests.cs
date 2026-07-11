using Core;
using Core.Domain;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace StressTests.CopyTrading;

// Stress on the self-healing copy-hosting lease: a horizontally scaled cluster of nodes contends for a
// pool of running copy profiles. Nodes die (stop renewing) and revive on a seeded random schedule; the
// invariants are that a live holder is never stolen from, a dead node's lease always lapses and gets
// reclaimed, and at most one node ever holds a valid lease on a profile at once. Time is driven by a
// FakeTimeProvider so lease-expiry boundaries are exact and reproducible.
public sealed class CopyLeaseReclaimStressTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(20); // renew interval < ttl

    private static CopyProfile RunningProfile()
    {
        var profile = CopyProfile.Create(UserId.New(), "p", TradingAccountId.New());
        profile.Start();
        return profile;
    }

    [Fact]
    public void Dead_node_lease_lapses_and_another_node_reclaims_exactly_after_expiry()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 07, 11, 12, 00, 00, TimeSpan.Zero));
        var profile = RunningProfile();
        var nodeA = new NodeIdentity("node-a");
        var nodeB = new NodeIdentity("node-b");

        profile.ClaimBy(nodeA, time.GetUtcNow() + Ttl);
        profile.IsLeaseHeldBy(nodeA, time.GetUtcNow()).Should().BeTrue();

        // node A dies. Just before expiry the lease is still A's and B must not be able to steal it.
        time.Advance(Ttl - TimeSpan.FromSeconds(1));
        Claimable(profile, time.GetUtcNow()).Should().BeFalse("a live lease must not be reclaimable");

        // exactly at expiry the lease lapses and B reclaims.
        time.Advance(TimeSpan.FromSeconds(1));
        Claimable(profile, time.GetUtcNow()).Should().BeTrue();
        profile.ClaimBy(nodeB, time.GetUtcNow() + Ttl);
        profile.IsLeaseHeldBy(nodeB, time.GetUtcNow()).Should().BeTrue();
        profile.IsLeaseHeldBy(nodeA, time.GetUtcNow()).Should().BeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(17)]
    [InlineData(42)]
    [InlineData(2718)]
    [InlineData(31337)]
    public void Cluster_of_nodes_never_double_holds_and_always_reclaims(int seed)
    {
        var random = new Random(seed);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 07, 11, 12, 00, 00, TimeSpan.Zero));

        var profiles = Enumerable.Range(0, 8).Select(_ => RunningProfile()).ToArray();
        var nodes = Enumerable.Range(0, 5).Select(i => new NodeIdentity($"node-{i}")).ToArray();
        var alive = nodes.ToDictionary(n => n, _ => true);

        for (var tick = 0; tick < 300; tick++)
        {
            var now = time.GetUtcNow();

            // random churn: kill or revive one node
            if (random.Next(100) < 25)
            {
                var target = nodes[random.Next(nodes.Length)];
                alive[target] = !alive[target];
            }

            // each alive node, in randomized order, renews what it holds then claims one free profile
            foreach (var node in nodes.OrderBy(_ => random.Next()))
            {
                if (!alive[node]) continue;
                foreach (var held in profiles.Where(p => p.IsLeaseHeldBy(node, now)))
                    held.RenewLease(now + Ttl);

                var free = profiles.FirstOrDefault(p => Claimable(p, now));
                if (free is not null) free.ClaimBy(node, now + Ttl);
            }

            // invariant: at most one node holds a valid lease on any profile at this instant
            foreach (var profile in profiles)
                nodes.Count(n => profile.IsLeaseHeldBy(n, now)).Should().BeLessThanOrEqualTo(1);

            time.Advance(Tick);
        }

        // settle: revive all nodes and let leases stabilise — every profile ends up held by exactly one node
        foreach (var node in nodes) alive[node] = true;
        for (var tick = 0; tick < 10; tick++)
        {
            var now = time.GetUtcNow();
            foreach (var node in nodes)
            {
                foreach (var held in profiles.Where(p => p.IsLeaseHeldBy(node, now)))
                    held.RenewLease(now + Ttl);
                var free = profiles.FirstOrDefault(p => Claimable(p, now));
                if (free is not null) free.ClaimBy(node, now + Ttl);
            }
            time.Advance(Tick);
        }

        var settled = time.GetUtcNow();
        foreach (var profile in profiles)
            nodes.Count(n => profile.IsLeaseHeldBy(n, settled)).Should().Be(1, "every profile must be reclaimed once the cluster is healthy");
    }

    // Mirror of CopyEngineSupervisor's claim predicate: a profile is free when unassigned or its lease lapsed.
    private static bool Claimable(CopyProfile profile, DateTimeOffset now)
        => profile.AssignedNode is null || profile.LeaseExpiresAt is null || profile.LeaseExpiresAt <= now;
}
