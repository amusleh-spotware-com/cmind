using Core;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public sealed class NodeDiscoveryTests
{
    private const string DataDir = "/var/app/data";

    [Fact]
    public void NodeEndpointUrl_trims_trailing_slash()
    {
        new NodeEndpointUrl("http://node-1:8080/").Value.Should().Be("http://node-1:8080");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("ftp://node-1:8080")]
    public void NodeEndpointUrl_rejects_invalid(string value)
    {
        var act = () => _ = new NodeEndpointUrl(value);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.NodeEndpointUrlInvalid);
    }

    [Fact]
    public void ClusterJoinToken_rejects_short_secret()
    {
        var act = () => _ = new ClusterJoinToken(new string('x', NodeAgentAuth.MinSecretLength - 1));
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.JoinTokenTooShort);
    }

    [Fact]
    public void ClusterJoinToken_accepts_min_length_secret()
    {
        var value = new string('x', NodeAgentAuth.MinSecretLength);
        new ClusterJoinToken(value).Value.Should().Be(value);
    }

    [Fact]
    public void SelfRegister_marks_reachable_and_raises_registered_event()
    {
        var node = RemoteNode.SelfRegister(NodeMode.Mixed, "node-1",
            new NodeEndpointUrl("http://node-1:8080"), [1, 2, 3], DataDir, 5);

        node.Should().BeOfType<ActiveMixedNode>();
        node.IsReachable.Should().BeTrue();
        node.LastHeartbeatAt.Should().NotBeNull();
        node.IsActive.Should().BeTrue();
        node.DomainEvents.OfType<NodeRegistered>().Should().ContainSingle();
    }

    [Fact]
    public void MarkUnreachable_disables_scheduling_and_raises_offline_event()
    {
        var node = NewNode();

        node.MarkUnreachable();

        node.IsReachable.Should().BeFalse();
        node.IsActive.Should().BeFalse();
        node.AcceptsRun.Should().BeFalse();
        node.AcceptsBacktest.Should().BeFalse();
        node.StatusName.Should().Be("Unreachable");
        node.DomainEvents.OfType<NodeWentOffline>().Should().ContainSingle();
    }

    [Fact]
    public void RecordHeartbeat_after_outage_brings_node_back_online()
    {
        var node = NewNode();
        node.MarkUnreachable();
        node.ClearDomainEvents();

        node.RecordHeartbeat(new NodeEndpointUrl("http://node-1:9090"), 12);

        node.IsReachable.Should().BeTrue();
        node.IsActive.Should().BeTrue();
        node.BaseUrl.Should().Be("http://node-1:9090");
        node.MaxInstances.Should().Be(12);
        node.DomainEvents.OfType<NodeCameOnline>().Should().ContainSingle();
    }

    [Fact]
    public void RecordHeartbeat_while_reachable_raises_no_online_event()
    {
        var node = NewNode();
        node.ClearDomainEvents();

        node.RecordHeartbeat(new NodeEndpointUrl("http://node-1:8080"), 5);

        node.DomainEvents.OfType<NodeCameOnline>().Should().BeEmpty();
    }

    [Fact]
    public void RecordHeartbeat_rejects_non_positive_max_instances()
    {
        var node = NewNode();

        var act = () => node.RecordHeartbeat(new NodeEndpointUrl("http://node-1:8080"), 0);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.NodeMaxInstancesInvalid);
    }

    [Fact]
    public void IsHeartbeatStale_true_only_past_ttl()
    {
        var node = NewNode();
        var last = node.LastHeartbeatAt!.Value;

        node.IsHeartbeatStale(TimeSpan.FromMinutes(1), last.AddSeconds(30)).Should().BeFalse();
        node.IsHeartbeatStale(TimeSpan.FromMinutes(1), last.AddMinutes(2)).Should().BeTrue();
    }

    [Fact]
    public void MarkUnreachable_is_idempotent()
    {
        var node = NewNode();
        node.MarkUnreachable();
        node.ClearDomainEvents();

        node.MarkUnreachable();

        node.DomainEvents.OfType<NodeWentOffline>().Should().BeEmpty();
    }

    private static RemoteNode NewNode()
        => RemoteNode.SelfRegister(NodeMode.Mixed, "node-1",
            new NodeEndpointUrl("http://node-1:8080"), [1, 2, 3], DataDir, 5);
}
