using Core;
using FluentAssertions;
using Nodes;
using Xunit;

namespace UnitTests;

public sealed class InstanceTransitionsTests
{
    [Fact]
    public void StoppedFrom_carries_container_and_identity_and_sets_stopped_time()
    {
        var startedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var stoppedAt = DateTimeOffset.UtcNow;
        var running = new RunningRunInstance
        {
            UserId = UserId.New(),
            CBotId = CBotId.New(),
            TradingAccountId = TradingAccountId.New(),
            NodeId = NodeId.New(),
            DockerImageTag = "1.2.3",
            Symbol = "EURUSD",
            Timeframe = "h1",
            ContainerId = "container-abc",
            StartedAt = startedAt,
            DataDirSubPath = "sub/path"
        };

        var stopped = InstanceTransitions.StoppedFrom(running, stoppedAt);

        stopped.UserId.Should().Be(running.UserId);
        stopped.CBotId.Should().Be(running.CBotId);
        stopped.TradingAccountId.Should().Be(running.TradingAccountId);
        stopped.ContainerId.Should().Be("container-abc");
        stopped.Symbol.Should().Be("EURUSD");
        stopped.StartedAt.Should().Be(startedAt);
        stopped.StoppedAt.Should().Be(stoppedAt);
        stopped.DataDirSubPath.Should().Be("sub/path");
    }
}
