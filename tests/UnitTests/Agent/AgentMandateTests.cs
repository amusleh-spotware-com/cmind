using Core;
using Core.Agent;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Agent;

// Invariants + transitions for the AgentMandate aggregate: create, enable/disable, run stamping,
// config setters, and owning AgentProposals through the root. (WS-1 Core backfill.)
public class AgentMandateTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 9, 0, 0, TimeSpan.Zero);

    private static AgentMandate NewMandate() => AgentMandate.Create(
        UserId.New(), CBotId.New(), "Alpha", "beat the market",
        new RiskPercent(1.0), new DrawdownPercent(20.0), new Symbol("EURUSD"), new Timeframe("h1"),
        new DockerImageTag("latest"), AgentAutonomy.Suggest, backtestSettingsJson: null);

    [Fact]
    public void Create_sets_defaults_and_starts_disabled()
    {
        var mandate = NewMandate();

        mandate.Name.Should().Be("Alpha");
        mandate.Enabled.Should().BeFalse();
        mandate.Autonomy.Should().Be(AgentAutonomy.Suggest);
        mandate.Proposals.Should().BeEmpty();
        mandate.LastRunAt.Should().BeNull();
    }

    [Fact]
    public void Create_rejects_a_blank_name()
    {
        var act = () => AgentMandate.Create(UserId.New(), CBotId.New(), " ", "obj",
            new RiskPercent(1.0), new DrawdownPercent(20.0), new Symbol("EURUSD"), new Timeframe("h1"),
            new DockerImageTag("latest"), AgentAutonomy.Suggest, null);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.NameRequired);
    }

    [Fact]
    public void Enable_and_disable_toggle_the_flag()
    {
        var mandate = NewMandate();

        mandate.Enable();
        mandate.Enabled.Should().BeTrue();

        mandate.Disable();
        mandate.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Record_run_stamps_the_time()
    {
        var mandate = NewMandate();
        mandate.RecordRun(Now);
        mandate.LastRunAt.Should().Be(Now);
    }

    [Fact]
    public void Config_setters_mutate_state()
    {
        var mandate = NewMandate();

        mandate.Rename("Beta");
        mandate.SetObjective("scalp");
        mandate.SetRiskPerTrade(new RiskPercent(2.5));
        mandate.SetMaxDrawdown(new DrawdownPercent(10));
        mandate.SetSymbol(new Symbol("GBPUSD"));
        mandate.SetTimeframe(new Timeframe("m15"));
        mandate.SetAutonomy(AgentAutonomy.Auto);
        var account = TradingAccountId.New();
        mandate.SetTradingAccount(account);

        mandate.Name.Should().Be("Beta");
        mandate.Objective.Should().Be("scalp");
        mandate.RiskPercentPerTrade.Should().Be(2.5);
        mandate.MaxDrawdownPercent.Should().Be(10);
        mandate.Symbol.Should().Be("GBPUSD");
        mandate.Timeframe.Should().Be("m15");
        mandate.Autonomy.Should().Be(AgentAutonomy.Auto);
        mandate.TradingAccountId.Should().Be(account);
    }

    [Fact]
    public void Rename_rejects_a_blank_name()
    {
        var mandate = NewMandate();
        var act = () => mandate.Rename("");
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.NameRequired);
    }

    [Fact]
    public void Set_objective_null_becomes_empty()
    {
        var mandate = NewMandate();
        mandate.SetObjective(null!);
        mandate.Objective.Should().BeEmpty();
    }

    [Fact]
    public void Add_proposal_owns_it_through_the_root()
    {
        var mandate = NewMandate();

        var proposal = mandate.AddProposal(AgentConstants.ProposalKindBacktest, "because", "{}", "prop-1");

        mandate.Proposals.Should().ContainSingle().Which.Should().BeSameAs(proposal);
        proposal.MandateId.Should().Be(mandate.Id);
        proposal.UserId.Should().Be(mandate.UserId);
        proposal.ProposedName.Should().Be("prop-1");
    }
}
