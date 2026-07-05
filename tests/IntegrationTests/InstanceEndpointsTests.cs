using Core;
using FluentAssertions;
using Web.Endpoints;
using Xunit;

namespace IntegrationTests;

public class InstanceEndpointsTests
{
    [Fact]
    public void GetStartedAt_reads_the_right_column_for_every_active_or_terminal_state()
    {
        var startedAt = DateTimeOffset.UtcNow.AddHours(-1);

        InstanceEndpoints.GetStartedAt(new RunningRunInstance { StartedAt = startedAt }).Should().Be(startedAt);
        InstanceEndpoints.GetStartedAt(new RunningBacktestInstance { StartedAt = startedAt }).Should().Be(startedAt);
        InstanceEndpoints.GetStartedAt(new StoppedRunInstance { StartedAt = startedAt }).Should().Be(startedAt);
        InstanceEndpoints.GetStartedAt(new CompletedBacktestInstance { StartedAt = startedAt }).Should().Be(startedAt);
        InstanceEndpoints.GetStartedAt(new FailedRunInstance { StartedAt = startedAt, FailureReason = "x" }).Should().Be(startedAt);
        InstanceEndpoints.GetStartedAt(new PendingRunInstance()).Should().BeNull();
    }

    [Fact]
    public void GetStoppedAt_reads_the_right_column_for_every_terminal_state()
    {
        var stoppedAt = DateTimeOffset.UtcNow;

        InstanceEndpoints.GetStoppedAt(new StoppedRunInstance { StoppedAt = stoppedAt }).Should().Be(stoppedAt);
        InstanceEndpoints.GetStoppedAt(new CompletedBacktestInstance { StoppedAt = stoppedAt }).Should().Be(stoppedAt);
        InstanceEndpoints.GetStoppedAt(new FailedBacktestInstance { StoppedAt = stoppedAt, FailureReason = "x" }).Should().Be(stoppedAt);
        InstanceEndpoints.GetStoppedAt(new RunningRunInstance { StartedAt = stoppedAt }).Should().BeNull();
    }
}
