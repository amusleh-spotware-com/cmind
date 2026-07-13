using Core;
using Core.Agent;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Agent;

// Fills the TradingAgent persona-config paths TradingAgentTests leaves out: rename, objective-weight
// clamp, temperament update, and a compiled prompt for every archetype. (WS-1 Core backfill.)
public class TradingAgentPersonaTests
{
    private static TradingAgent NewAgent(AgentArchetype archetype = AgentArchetype.Scalper)
        => TradingAgent.Create(UserId.New(), "Nova", archetype, AgentTemperament.Balanced);

    [Fact]
    public void Rename_changes_the_name_and_rejects_blank()
    {
        var agent = NewAgent();

        agent.Rename("Vega");
        agent.Name.Should().Be("Vega");

        var act = () => agent.Rename(" ");
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.AgentNameRequired);
    }

    [Fact]
    public void Objective_drawdown_weight_is_clamped_to_the_unit_interval()
    {
        var agent = NewAgent();

        agent.SetObjectiveDrawdownWeight(2.0);
        agent.ObjectiveDrawdownWeight.Should().Be(1.0);

        agent.SetObjectiveDrawdownWeight(-1.0);
        agent.ObjectiveDrawdownWeight.Should().Be(0.0);

        agent.SetObjectiveDrawdownWeight(0.3);
        agent.ObjectiveDrawdownWeight.Should().Be(0.3);
    }

    [Fact]
    public void Set_temperament_validates_and_updates()
    {
        var agent = NewAgent();

        agent.SetTemperament(new AgentTemperament(0.9, 0.1, 0.7));
        agent.Temperament.Should().Be(new AgentTemperament(0.9, 0.1, 0.7));

        var bad = () => agent.SetTemperament(new AgentTemperament(0.5, double.NaN, 0.5));
        bad.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.AgentTemperamentInvalid);
    }

    [Theory]
    [InlineData(AgentArchetype.Scalper)]
    [InlineData(AgentArchetype.DayTrader)]
    [InlineData(AgentArchetype.SwingTrader)]
    [InlineData(AgentArchetype.PositionTrader)]
    [InlineData(AgentArchetype.NewsTrader)]
    [InlineData(AgentArchetype.Contrarian)]
    [InlineData(AgentArchetype.MeanReversion)]
    [InlineData(AgentArchetype.BreakoutMomentum)]
    public void Compile_system_prompt_briefs_every_archetype(AgentArchetype archetype)
    {
        var prompt = NewAgent(archetype).CompileSystemPrompt();

        prompt.Should().Contain(archetype.ToString());
        prompt.Should().Contain("Temperament").And.Contain("risk envelope");
    }
}
