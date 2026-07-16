using Core;
using FluentAssertions;
using Xunit;

namespace UnitTests;

// The BacktestInstance TPH state machine: starting -> running -> completed/failed, replacing the entity
// each step and carrying execution state + backtest settings across. (WS-1 Core backfill.)
public class BacktestInstanceTransitionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 14, 0, 0, TimeSpan.Zero);

    private static StartingBacktestInstance NewStarting()
        => BacktestInstance.CreateStarting(UserId.New(), CBotId.New(), NodeId.New(),
            new DockerImageTag("latest"), new Symbol("EURUSD"), new Timeframe("h1"), "{\"from\":\"2024\"}");

    [Fact]
    public void Create_starting_is_active_backtest_carrying_settings()
    {
        var starting = NewStarting();

        starting.KindName.Should().Be("Backtest");
        starting.StatusName.Should().Be("Starting");
        starting.IsActive.Should().BeTrue();
        starting.IsTerminal.Should().BeFalse();
        starting.BacktestSettingsJson.Should().Be("{\"from\":\"2024\"}");
    }

    [Fact]
    public void Starting_to_running_carries_settings_and_container()
    {
        var starting = NewStarting();

        var running = starting.ToRunning("c-1", Now);

        running.StatusName.Should().Be("Running");
        running.IsActive.Should().BeTrue();
        running.ContainerId.Should().Be("c-1");
        running.StartedAt.Should().Be(Now);
        running.BacktestSettingsJson.Should().Be(starting.BacktestSettingsJson);
        running.UserId.Should().Be(starting.UserId);
        running.Symbol.Should().Be(starting.Symbol);
    }

    [Fact]
    public void Running_to_completed_is_terminal_with_report()
    {
        var running = NewStarting().ToRunning("c-1", Now);

        var completed = running.ToCompleted(Now.AddMinutes(10), reportJson: "{\"pnl\":42}", resultJsonPath: "/r.json");

        completed.StatusName.Should().Be("Completed");
        completed.IsTerminal.Should().BeTrue();
        completed.IsActive.Should().BeFalse();
        completed.ReportJson.Should().Be("{\"pnl\":42}");
        completed.ResultJsonPath.Should().Be("/r.json");
        completed.StoppedAt.Should().Be(Now.AddMinutes(10));
        completed.ContainerId.Should().Be("c-1");
    }

    [Fact]
    public void Running_to_failed_is_terminal_and_records_the_reason()
    {
        var running = NewStarting().ToRunning("c-1", Now);

        var failed = running.ToFailed("build error", Now.AddMinutes(2));

        failed.StatusName.Should().Be("Failed");
        failed.IsTerminal.Should().BeTrue();
        failed.FailureReason.Should().Be("build error");
        failed.UserId.Should().Be(running.UserId);
    }

    [Fact]
    public void Create_starting_without_a_param_set_is_allowed_and_carries_the_account()
    {
        var accountId = TradingAccountId.New();
        var starting = BacktestInstance.CreateStarting(UserId.New(), CBotId.New(), NodeId.New(),
            new DockerImageTag("latest"), new Symbol("EURUSD"), new Timeframe("h1"), "{}", accountId, paramSetId: null);

        starting.ParamSetId.Should().BeNull("a backtest with no parameter set uses the cBot's default values");
        starting.TradingAccountId.Should().Be(accountId);
    }

    [Fact]
    public void Captured_console_log_survives_completion_and_failure()
    {
        var completed = NewStarting().ToRunning("c-1", Now);
        completed.CaptureConsoleLog("bt line 1\nbt line 2");
        completed.ToCompleted(Now.AddMinutes(1)).ConsoleLog.Should().Be("bt line 1\nbt line 2",
            "a completed backtest keeps its captured console log so it stays downloadable");

        var failing = NewStarting().ToRunning("c-2", Now);
        failing.CaptureConsoleLog("boom");
        failing.ToFailed("err", Now.AddMinutes(1)).ConsoleLog.Should().Be("boom");
    }

    [Fact]
    public void Create_failed_produces_a_terminal_failed_backtest()
    {
        var failed = BacktestInstance.CreateFailed(UserId.New(), CBotId.New(), NodeId.New(),
            new DockerImageTag("latest"), new Symbol("EURUSD"), new Timeframe("h1"), "no node");

        failed.StatusName.Should().Be("Failed");
        failed.IsTerminal.Should().BeTrue();
        failed.FailureReason.Should().Be("no node");
    }
}
