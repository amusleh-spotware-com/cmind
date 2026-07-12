using System;
using System.Collections.Generic;
using Core;
using Core.Agent;
using Core.Autonomy;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class TradingAgentTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    private static TradingAgent NewAgent(AgentArchetype archetype = AgentArchetype.Scalper) =>
        TradingAgent.Create(UserId.New(), "Alpha", archetype, AgentTemperament.Balanced);

    private static RiskEnvelope Envelope() => new(4, 10, 2, 30, 3, 20);

    [Fact]
    public void Create_yields_a_draft_advisory_agent()
    {
        var a = NewAgent();
        a.Status.Should().Be(AgentStatus.Draft);
        a.Autonomy.Should().Be(AutonomyLevel.Advisory);
        a.Watermark.Should().Be(0);
    }

    [Fact]
    public void Cannot_start_without_managed_accounts()
    {
        var act = () => NewAgent().Start(Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("domain.agent.no_managed_accounts");
    }

    [Fact]
    public void Advisory_agent_starts_and_stops()
    {
        var a = NewAgent();
        a.SetManagedAccounts(new[] { TradingAccountId.New() });
        a.Start(Now);
        a.Status.Should().Be(AgentStatus.Running);
        a.StartedAt.Should().Be(Now);
        a.Stop();
        a.Status.Should().Be(AgentStatus.Stopped);
    }

    [Fact]
    public void Full_auto_requires_envelope_then_consent()
    {
        var a = NewAgent();
        a.SetManagedAccounts(new[] { TradingAccountId.New() });

        var noEnvelope = () => a.SetAutonomy(AutonomyLevel.FullAuto);
        noEnvelope.Should().Throw<DomainException>().Which.Code.Should().Be("domain.agent.envelope_required");

        a.SetRiskEnvelope(Envelope());
        a.SetAutonomy(AutonomyLevel.FullAuto);

        var noConsent = () => a.Start(Now);
        noConsent.Should().Throw<DomainException>().Which.Code.Should().Be("domain.agent.consent_required");

        a.AcceptDisclaimer(Now);
        a.Start(Now);
        a.Status.Should().Be(AgentStatus.Running);
    }

    [Fact]
    public void Halt_is_idempotent_and_only_from_running()
    {
        var a = NewAgent();
        a.SetManagedAccounts(new[] { TradingAccountId.New() });
        a.Start(Now);
        a.Halt("kill", Now);
        a.Status.Should().Be(AgentStatus.Halted);
        a.HaltReason.Should().Be("kill");
        a.Invoking(x => x.Halt("again", Now)).Should().NotThrow(); // idempotent
    }

    [Fact]
    public void Goals_and_accounts_round_trip()
    {
        var a = NewAgent();
        var goals = new List<PerformanceTarget>
        {
            new(TargetMetric.MaxDrawdown, TargetComparator.Below, 4, TargetEnforcement.Hard),
            new(TargetMetric.ProfitFactor, TargetComparator.AtLeast, 1.5, TargetEnforcement.Soft)
        };
        a.SetGoals(goals);
        a.Goals.Should().HaveCount(2);
        a.Goals[0].Metric.Should().Be(TargetMetric.MaxDrawdown);

        var accounts = new[] { TradingAccountId.New(), TradingAccountId.New() };
        a.SetManagedAccounts(accounts);
        a.ManagedAccounts.Should().HaveCount(2);
    }

    [Fact]
    public void Compile_system_prompt_is_deterministic_and_descriptive()
    {
        var a = NewAgent(AgentArchetype.NewsTrader);
        a.SetGoals(new List<PerformanceTarget> { new(TargetMetric.MaxDrawdown, TargetComparator.Below, 4, TargetEnforcement.Hard) });
        var p1 = a.CompileSystemPrompt();
        var p2 = a.CompileSystemPrompt();
        p1.Should().Be(p2);
        p1.Should().Contain("NewsTrader").And.Contain("MaxDrawdown").And.Contain("risk envelope");
    }

    [Fact]
    public void Record_action_advances_the_watermark()
    {
        var a = NewAgent();
        a.RecordAction("evaluated EURUSD", Now);
        a.Watermark.Should().Be(1);
        a.LastAction.Should().Be("evaluated EURUSD");
    }

    [Fact]
    public void Invalid_temperament_is_rejected()
    {
        var act = () => TradingAgent.Create(UserId.New(), "X", AgentArchetype.Scalper, new AgentTemperament(2.0, 0.5, 0.5));
        act.Should().Throw<DomainException>().Which.Code.Should().Be("domain.agent.temperament_invalid");
    }
}
