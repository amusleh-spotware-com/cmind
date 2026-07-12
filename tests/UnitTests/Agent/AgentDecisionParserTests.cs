using Core;
using Core.Agent;
using Core.Execution;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class AgentDecisionParserTests
{
    private static readonly TradingAccountId Account = TradingAccountId.New();

    [Fact]
    public void Parses_a_buy_order()
    {
        var parsed = AgentDecisionParser.Parse(
            """{ "reasoning": "momentum up", "action": "buy", "symbol": "eurusd", "sizeLots": 1.5, "evidence": ["backtest:1"] }""");
        var decision = AgentDecisionParser.ToDecision(parsed, Account);

        decision.Order.Should().NotBeNull();
        decision.Order!.Side.Should().Be(OrderSide.Buy);
        decision.Order.Symbol.Should().Be("EURUSD");
        decision.Order.SizeLots.Should().Be(1.5);
        decision.Evidence.Should().ContainSingle().Which.Should().Be("backtest:1");
    }

    [Fact]
    public void Parses_json_inside_code_fences()
    {
        var parsed = AgentDecisionParser.Parse("```json\n{ \"action\": \"sell\", \"symbol\": \"GBPUSD\", \"sizeLots\": 2 }\n```");
        var decision = AgentDecisionParser.ToDecision(parsed, Account);
        decision.Order!.Side.Should().Be(OrderSide.Sell);
    }

    [Fact]
    public void Hold_action_yields_no_order()
    {
        var decision = AgentDecisionParser.ToDecision(
            AgentDecisionParser.Parse("""{ "reasoning": "waiting", "action": "hold" }"""), Account);
        decision.Order.Should().BeNull();
        decision.Reasoning.Should().Be("waiting");
    }

    [Fact]
    public void Malformed_text_degrades_to_a_hold()
    {
        var decision = AgentDecisionParser.ToDecision(AgentDecisionParser.Parse("I think we should buy soon."), Account);
        decision.Order.Should().BeNull();
    }

    [Fact]
    public void Missing_size_or_symbol_is_not_actionable()
    {
        AgentDecisionParser.ToDecision(AgentDecisionParser.Parse("""{ "action": "buy", "symbol": "EURUSD" }"""), Account)
            .Order.Should().BeNull();
        AgentDecisionParser.ToDecision(AgentDecisionParser.Parse("""{ "action": "buy", "sizeLots": 1 }"""), Account)
            .Order.Should().BeNull();
    }
}
