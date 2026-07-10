using Core;
using Core.Agent;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public sealed class DomainInvariantsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void RiskPercent_rejects_out_of_range(double value)
    {
        var act = () => _ = new RiskPercent(value);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.RiskOutOfRange);
    }

    [Fact]
    public void EvaluationInterval_rejects_below_minimum()
    {
        var act = () => _ = new EvaluationInterval(AlertConstants.MinIntervalMinutes - 1);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.IntervalOutOfRange);
    }

    [Fact]
    public void AlertSeverity_rejects_unknown_value()
    {
        var act = () => _ = new AlertSeverity("catastrophic");
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.SeverityUnknown);
    }

    [Fact]
    public void AlertRule_Raise_when_disabled_throws()
    {
        var rule = AlertRule.Create(UserId.New(), "eur watch", new Symbol("EURUSD"), new EvaluationInterval(30));
        rule.Disable();

        var act = () => rule.Raise(AlertSeverity.Warning, "spike", TestClock.Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.AlertRuleDisabled);
    }

    [Fact]
    public void AlertRule_Raise_appends_event_and_raises_domain_event()
    {
        var rule = AlertRule.Create(UserId.New(), "eur watch", new Symbol("EURUSD"), new EvaluationInterval(30));

        var evt = rule.Raise(AlertSeverity.Critical, "ECB surprise", TestClock.Now);

        evt.Severity.Should().Be(AlertConstants.SeverityCritical);
        rule.Events.Should().ContainSingle();
        rule.DomainEvents.OfType<AlertRaised>().Should().ContainSingle();
    }

    [Fact]
    public void RunInstance_start_transition_carries_identity_and_raises_started_event()
    {
        var starting = RunInstance.CreateStarting(UserId.New(), CBotId.New(), NodeId.New(),
            DockerImageTag.Latest, new Symbol("EURUSD"), new Timeframe("h1"));

        var running = starting.ToRunning("container-1", TestClock.Now);

        running.ContainerId.Should().Be("container-1");
        running.CBotId.Should().Be(starting.CBotId);
        running.Symbol.Should().Be("EURUSD");
        running.IsActive.Should().BeTrue();
        running.DomainEvents.OfType<InstanceStarted>().Should().ContainSingle();
    }

    [Fact]
    public void RunningInstance_stop_transition_produces_terminal_state()
    {
        var running = RunInstance.CreateStarting(UserId.New(), CBotId.New(), NodeId.New(),
            DockerImageTag.Latest, new Symbol("EURUSD"), new Timeframe("h1")).ToRunning("c", TestClock.Now);

        var stopped = running.ToStopped(TestClock.Now);

        stopped.IsTerminal.Should().BeTrue();
        stopped.ContainerId.Should().Be("c");
        stopped.DomainEvents.OfType<InstanceStopped>().Should().ContainSingle();
    }

    [Fact]
    public void McpApiKey_double_revoke_throws()
    {
        var key = McpApiKey.Create(UserId.New(), "prefix0000000000", "hash", "label");
        key.Revoke(TestClock.Now);

        var act = () => key.Revoke(TestClock.Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.McpKeyAlreadyRevoked);
    }

    [Fact]
    public void AgentProposal_reject_after_decided_throws()
    {
        var mandate = AgentMandate.Create(UserId.New(), CBotId.New(), "m", "obj",
            new RiskPercent(1), new DrawdownPercent(20), new Symbol("EURUSD"), new Timeframe("h1"),
            DockerImageTag.Latest, AgentAutonomy.Suggest, null);
        var proposal = mandate.AddProposal("Backtest", "reason", "{}", "name");
        proposal.Reject(UserId.New(), TestClock.Now);

        var act = () => proposal.Reject(UserId.New(), TestClock.Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.ProposalNotPending);
    }
}
