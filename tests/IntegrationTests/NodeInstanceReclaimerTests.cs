using Core;
using Core.Domain;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nodes;
using Xunit;

namespace IntegrationTests;

public class NodeInstanceReclaimerTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string DataDir = "/var/app/data";
    private static readonly DateTimeOffset Now = new(2026, 07, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Threshold = TimeSpan.FromSeconds(150);

    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System))
            .Options);

    [Fact]
    public async Task ReclaimAsync_fails_running_instance_on_unreachable_node()
    {
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();

        var user = RegularUser.Create(new Email($"reclaim-{Guid.NewGuid():N}@test.invalid"), "hash", [1], false);
        var cbot = CBot.Create(user.Id, "bot", [1, 2, 3]);
        var node = CtraderCliNode.SelfRegister(NodeMode.Mixed, $"node-{Guid.NewGuid():N}",
            new NodeEndpointUrl("http://node-1:8080"), [1, 2, 3], DataDir, 5, Now);
        node.MarkUnreachable();

        var instance = new RunningRunInstance
        {
            UserId = user.Id,
            CBotId = cbot.Id,
            DockerImageTag = "1.0.0",
            Symbol = "EURUSD",
            Timeframe = "h1",
            ContainerId = "container-1",
            StartedAt = Now
        };
        instance.AttachNode(node);

        await using (var seed = CreateContext())
        {
            seed.Users.Add(user);
            seed.CBots.Add(cbot);
            seed.Nodes.Add(node);
            seed.Instances.Add(instance);
            await seed.SaveChangesAsync();
        }

        await using (var act = CreateContext())
        {
            var reclaimedNow = Now + Threshold + TimeSpan.FromSeconds(1);
            var count = await NodeInstanceReclaimer.ReclaimAsync(
                act, Threshold, reclaimedNow, NullLogger.Instance, CancellationToken.None);
            count.Should().Be(1);
        }

        await using var verify = CreateContext();
        var failed = await verify.Instances.OfType<FailedRunInstance>().ToListAsync();
        failed.Should().ContainSingle();
        failed[0].FailureReason.Should().Be(NodeInstanceReclaimer.NodeUnreachableReason);
        (await verify.Instances.OfType<RunningRunInstance>().AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task ReclaimAsync_leaves_instance_on_reachable_node_untouched()
    {
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();

        var user = RegularUser.Create(new Email($"reclaim-{Guid.NewGuid():N}@test.invalid"), "hash", [1], false);
        var cbot = CBot.Create(user.Id, "bot", [1, 2, 3]);
        var node = CtraderCliNode.SelfRegister(NodeMode.Mixed, $"node-{Guid.NewGuid():N}",
            new NodeEndpointUrl("http://node-2:8080"), [1, 2, 3], DataDir, 5, Now);

        var instance = new RunningRunInstance
        {
            UserId = user.Id,
            CBotId = cbot.Id,
            DockerImageTag = "1.0.0",
            Symbol = "EURUSD",
            Timeframe = "h1",
            ContainerId = "container-2",
            StartedAt = Now
        };
        instance.AttachNode(node);

        await using (var seed = CreateContext())
        {
            seed.Users.Add(user);
            seed.CBots.Add(cbot);
            seed.Nodes.Add(node);
            seed.Instances.Add(instance);
            await seed.SaveChangesAsync();
        }

        await using var act = CreateContext();
        var reclaimedNow = Now + Threshold + TimeSpan.FromSeconds(1);
        var count = await NodeInstanceReclaimer.ReclaimAsync(
            act, Threshold, reclaimedNow, NullLogger.Instance, CancellationToken.None);

        count.Should().Be(0);
        (await act.Instances.OfType<RunningRunInstance>().CountAsync(i => i.Id == instance.Id)).Should().Be(1);
    }
}
