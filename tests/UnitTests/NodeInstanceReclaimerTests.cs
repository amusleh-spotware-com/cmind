using Core;
using Core.Domain;
using FluentAssertions;
using Nodes;
using Xunit;

namespace UnitTests;

public sealed class NodeInstanceReclaimerTests
{
    private const string DataDir = "/var/app/data";
    private static readonly TimeSpan Threshold = TimeSpan.FromSeconds(150);

    private static Core.CtraderCliNode UnreachableNode(DateTimeOffset lastHeartbeat)
    {
        var node = Core.CtraderCliNode.SelfRegister(NodeMode.Mixed, "node-1",
            new NodeEndpointUrl("http://node-1:8080"), [1, 2, 3], DataDir, 5, lastHeartbeat);
        node.MarkUnreachable();
        return node;
    }

    private static RunningRunInstance RunningOn(Node node)
    {
        var instance = new RunningRunInstance
        {
            UserId = UserId.New(),
            CBotId = CBotId.New(),
            DockerImageTag = "1.0.0",
            Symbol = "EURUSD",
            Timeframe = "h1",
            ContainerId = "container-1",
            StartedAt = TestClock.Now
        };
        instance.AttachNode(node);
        return instance;
    }

    [Fact]
    public void ShouldReclaim_running_instance_on_stale_unreachable_node()
    {
        var node = UnreachableNode(TestClock.Now);
        var instance = RunningOn(node);
        var now = TestClock.Now + Threshold + TimeSpan.FromSeconds(1);

        NodeInstanceReclaimer.ShouldReclaim(instance, Threshold, now).Should().BeTrue();
    }

    [Fact]
    public void ShouldNotReclaim_when_node_still_within_threshold()
    {
        var node = UnreachableNode(TestClock.Now);
        var instance = RunningOn(node);
        var now = TestClock.Now + Threshold - TimeSpan.FromSeconds(1);

        NodeInstanceReclaimer.ShouldReclaim(instance, Threshold, now).Should().BeFalse();
    }

    [Fact]
    public void ShouldNotReclaim_when_node_reachable()
    {
        var node = Core.CtraderCliNode.SelfRegister(NodeMode.Mixed, "node-1",
            new NodeEndpointUrl("http://node-1:8080"), [1, 2, 3], DataDir, 5, TestClock.Now);
        var instance = RunningOn(node);
        var now = TestClock.Now + Threshold + TimeSpan.FromSeconds(1);

        NodeInstanceReclaimer.ShouldReclaim(instance, Threshold, now).Should().BeFalse();
    }

    [Fact]
    public void ShouldNotReclaim_terminal_instance()
    {
        var node = UnreachableNode(TestClock.Now);
        var running = RunningOn(node);
        var stopped = running.ToStopped(TestClock.Now);
        stopped.AttachNode(node);
        var now = TestClock.Now + Threshold + TimeSpan.FromSeconds(1);

        NodeInstanceReclaimer.ShouldReclaim(stopped, Threshold, now).Should().BeFalse();
    }

    [Fact]
    public void ShouldNotReclaim_local_node_instance()
    {
        var node = LocalNode.Create("local", DataDir, 5, enabled: true);
        var instance = RunningOn(node);
        var now = TestClock.Now + Threshold + TimeSpan.FromSeconds(1);

        NodeInstanceReclaimer.ShouldReclaim(instance, Threshold, now).Should().BeFalse();
    }
}
