using Core;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Nodes;

// Invariants + state for the Node TPH: mode-driven creation, InitCore guards, heartbeat reachability
// transitions, and the LocalNode enable state. (WS-1 Core backfill.)
public class NodeHierarchyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 22, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Secret = [1, 2];

    private static Core.CtraderCliNode NewNode(NodeMode mode) =>
        Core.CtraderCliNode.Create(mode, "node-1", "http://node:8080/", Secret, "/data", maxInstances: 5);

    [Fact]
    public void Run_mode_node_accepts_runs_only_and_trims_the_url()
    {
        var node = NewNode(NodeMode.Run);

        node.ModeName.Should().Be("Run");
        node.IsLocal.Should().BeFalse();
        node.IsActive.Should().BeTrue();
        node.StatusName.Should().Be("Active");
        node.AcceptsRun.Should().BeTrue();
        node.AcceptsBacktest.Should().BeFalse();
        node.BaseUrl.Should().Be("http://node:8080", "the trailing slash is trimmed");
    }

    [Fact]
    public void Backtest_and_mixed_modes_accept_the_right_work()
    {
        NewNode(NodeMode.Backtest).AcceptsBacktest.Should().BeTrue();
        NewNode(NodeMode.Backtest).AcceptsRun.Should().BeFalse();

        var mixed = NewNode(NodeMode.Mixed);
        mixed.AcceptsRun.Should().BeTrue();
        mixed.AcceptsBacktest.Should().BeTrue();
    }

    [Fact]
    public void Create_guards_max_instances_and_name()
    {
        var badMax = () => Core.CtraderCliNode.Create(NodeMode.Run, "n", "http://n", Secret, "/d", 0);
        badMax.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.NodeMaxInstancesInvalid);

        var blankName = () => Core.CtraderCliNode.Create(NodeMode.Run, " ", "http://n", Secret, "/d", 5);
        blankName.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.NameRequired);
    }

    [Fact]
    public void Mark_unreachable_then_heartbeat_restores_reachability()
    {
        var node = NewNode(NodeMode.Mixed);

        node.MarkUnreachable();
        node.IsActive.Should().BeFalse();
        node.StatusName.Should().Be("Unreachable");
        node.AcceptsRun.Should().BeFalse();

        node.RecordHeartbeat(new NodeEndpointUrl("http://node:9090"), maxInstances: 8, Now);
        node.IsActive.Should().BeTrue();
        node.StatusName.Should().Be("Active");
        node.BaseUrl.Should().Be("http://node:9090");
        node.MaxInstances.Should().Be(8);
    }

    [Fact]
    public void Heartbeat_guards_max_instances_and_staleness_is_time_based()
    {
        var node = NewNode(NodeMode.Run);
        var badMax = () => node.RecordHeartbeat(new NodeEndpointUrl("http://n"), 0, Now);
        badMax.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.NodeMaxInstancesInvalid);

        node.RecordHeartbeat(new NodeEndpointUrl("http://n"), 5, Now);
        node.IsHeartbeatStale(TimeSpan.FromMinutes(1), Now.AddSeconds(30)).Should().BeFalse();
        node.IsHeartbeatStale(TimeSpan.FromMinutes(1), Now.AddMinutes(2)).Should().BeTrue();
    }

    [Fact]
    public void Local_node_state_follows_the_enabled_flag()
    {
        var node = LocalNode.Create("local", "/data", 3, enabled: true);

        node.IsLocal.Should().BeTrue();
        node.IsActive.Should().BeTrue();
        node.StatusName.Should().Be("Active");

        node.SetEnabled(false);
        node.IsActive.Should().BeFalse();
        node.StatusName.Should().Be("Disabled");
        node.AcceptsRun.Should().BeFalse();
    }
}
