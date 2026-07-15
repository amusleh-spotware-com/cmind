using Core;
using FluentAssertions;
using Xunit;

namespace UnitTests;

// The RunInstance TPH state machine: a transition REPLACES the entity with its next state, carrying the
// execution identity (user/cbot/symbol/…) and container across, with correct terminal/active flags.
// (WS-1 Core backfill; complements InstanceTransitionsTests.)
public class RunInstanceTransitionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 13, 0, 0, TimeSpan.Zero);

    private static StartingRunInstance NewStarting()
        => RunInstance.CreateStarting(UserId.New(), CBotId.New(), NodeId.New(),
            new DockerImageTag("latest"), new Symbol("EURUSD"), new Timeframe("h1"));

    [Fact]
    public void Create_starting_is_active_non_terminal_run()
    {
        var starting = NewStarting();

        starting.KindName.Should().Be("Run");
        starting.StatusName.Should().Be("Starting");
        starting.IsActive.Should().BeTrue();
        starting.IsTerminal.Should().BeFalse();
        starting.Symbol.Should().Be("EURUSD");
        starting.Timeframe.Should().Be("h1");
    }

    [Fact]
    public void Starting_to_running_carries_execution_state_and_container()
    {
        var starting = NewStarting();

        var running = starting.ToRunning("container-1", Now);

        running.StatusName.Should().Be("Running");
        running.IsActive.Should().BeTrue();
        running.IsTerminal.Should().BeFalse();
        running.ContainerId.Should().Be("container-1");
        running.StartedAt.Should().Be(Now);
        running.UserId.Should().Be(starting.UserId);
        running.CBotId.Should().Be(starting.CBotId);
        running.Symbol.Should().Be(starting.Symbol);
        running.Timeframe.Should().Be(starting.Timeframe);
    }

    [Fact]
    public void Running_to_stopped_is_terminal_and_keeps_the_container()
    {
        var running = NewStarting().ToRunning("container-1", Now);

        var stopped = running.ToStopped(Now.AddMinutes(30));

        stopped.StatusName.Should().Be("Stopped");
        stopped.IsTerminal.Should().BeTrue();
        stopped.IsActive.Should().BeFalse();
        stopped.ContainerId.Should().Be("container-1");
        stopped.StoppedAt.Should().Be(Now.AddMinutes(30));
        stopped.UserId.Should().Be(running.UserId);
    }

    [Fact]
    public void Starting_to_stopped_is_terminal()
    {
        var stopped = NewStarting().ToStopped(Now);

        stopped.StatusName.Should().Be("Stopped");
        stopped.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void Running_to_failed_is_terminal_and_records_the_reason()
    {
        var running = NewStarting().ToRunning("container-1", Now);

        var failed = running.ToFailed("node died", Now.AddMinutes(5));

        failed.StatusName.Should().Be("Failed");
        failed.IsTerminal.Should().BeTrue();
        failed.IsActive.Should().BeFalse();
        failed.FailureReason.Should().Be("node died");
        failed.ContainerId.Should().Be("container-1");
        failed.UserId.Should().Be(running.UserId);
    }

    [Fact]
    public void Create_starting_carries_the_chosen_account_and_param_set_across_to_running()
    {
        var accountId = TradingAccountId.New();
        var paramSetId = ParamSetId.New();
        var starting = RunInstance.CreateStarting(UserId.New(), CBotId.New(), NodeId.New(),
            new DockerImageTag("latest"), new Symbol("EURUSD"), new Timeframe("h1"), accountId, paramSetId);

        starting.TradingAccountId.Should().Be(accountId);
        starting.ParamSetId.Should().Be(paramSetId);

        var running = starting.ToRunning("container-1", Now);
        running.TradingAccountId.Should().Be(accountId, "the selected account must survive the state transition");
        running.ParamSetId.Should().Be(paramSetId, "the selected parameter set must survive the state transition");
    }

    [Fact]
    public void Create_starting_without_an_account_or_param_set_is_allowed()
    {
        var starting = NewStarting();
        starting.TradingAccountId.Should().BeNull();
        starting.ParamSetId.Should().BeNull("a run with no parameter set uses the cBot's default parameter values");
    }

    [Fact]
    public void Create_failed_produces_a_terminal_failed_instance()
    {
        var failed = RunInstance.CreateFailed(UserId.New(), CBotId.New(), NodeId.New(),
            new DockerImageTag("latest"), new Symbol("EURUSD"), new Timeframe("h1"), "no node available");

        failed.StatusName.Should().Be("Failed");
        failed.IsTerminal.Should().BeTrue();
        failed.FailureReason.Should().Be("no node available");
    }
}
