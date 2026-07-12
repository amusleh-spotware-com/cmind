using System;
using Core;
using Core.Agent;
using Core.Autonomy;
using Core.Execution;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class AgentDecisionProcessorTests
{
    private readonly AgentDecisionProcessor _processor = new();
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly TradingAccountId Account = TradingAccountId.New();

    private static TradingAgent FullAuto()
    {
        var a = TradingAgent.Create(UserId.New(), "Auto", AgentArchetype.Scalper, AgentTemperament.Balanced);
        a.SetManagedAccounts([Account]);
        a.SetRiskEnvelope(new RiskEnvelope(4, 10, 2, 30, 3, 20));
        a.SetAutonomy(AutonomyLevel.FullAuto);
        a.AcceptDisclaimer(Now);
        return a;
    }

    private static TradingAgent Advisory()
    {
        var a = TradingAgent.Create(UserId.New(), "Advice", AgentArchetype.SwingTrader, AgentTemperament.Balanced);
        a.SetManagedAccounts([Account]);
        return a;
    }

    private static AccountState State(int losses = 0, double dailyLoss = 0, double open = 0, int orders = 0) =>
        new(Account, 10_000m, 10_000m, open, losses, dailyLoss, orders);

    private static AgentDecision Order(double size) =>
        new("buy signal", new AgentOrderIntent(Account, "EURUSD", OrderSide.Buy, size), []);

    private static AgentDecision Hold() => new("waiting", null, []);

    [Fact]
    public void Advisory_agent_never_auto_executes()
    {
        var r = _processor.Process(Advisory(), State(), Order(1), aiAvailable: true, hardGoalBreached: false);
        r.Outcome.Should().Be(DecisionOutcome.Held);
        r.ShouldExecute.Should().BeFalse();
    }

    [Fact]
    public void Full_auto_clears_a_compliant_order()
    {
        var r = _processor.Process(FullAuto(), State(open: 2), Order(1), aiAvailable: true, hardGoalBreached: false);
        r.Outcome.Should().Be(DecisionOutcome.Cleared);
        r.ShouldExecute.Should().BeTrue();
    }

    [Fact]
    public void Full_auto_rejects_an_oversized_order()
    {
        var r = _processor.Process(FullAuto(), State(), Order(3), aiAvailable: true, hardGoalBreached: false);
        r.Outcome.Should().Be(DecisionOutcome.RejectedByEnvelope);
        r.ShouldExecute.Should().BeFalse();
    }

    [Fact]
    public void Ai_unavailable_halts_even_full_auto()
    {
        var r = _processor.Process(FullAuto(), State(), Order(1), aiAvailable: false, hardGoalBreached: false);
        r.Outcome.Should().Be(DecisionOutcome.HaltedByBreaker);
        r.ShouldHalt.Should().BeTrue();
        r.ShouldExecute.Should().BeFalse();
    }

    [Fact]
    public void Hard_goal_breach_halts()
    {
        var r = _processor.Process(FullAuto(), State(), Order(1), aiAvailable: true, hardGoalBreached: true);
        r.Outcome.Should().Be(DecisionOutcome.HaltedByBreaker);
    }

    [Fact]
    public void Loss_streak_and_daily_loss_halt()
    {
        _processor.Process(FullAuto(), State(losses: 3), Order(1), true, false).ShouldHalt.Should().BeTrue();
        _processor.Process(FullAuto(), State(dailyLoss: 4), Order(1), true, false).ShouldHalt.Should().BeTrue();
    }

    [Fact]
    public void No_order_is_held()
    {
        var r = _processor.Process(FullAuto(), State(), Hold(), aiAvailable: true, hardGoalBreached: false);
        r.Outcome.Should().Be(DecisionOutcome.Held);
        r.ShouldExecute.Should().BeFalse();
    }
}
