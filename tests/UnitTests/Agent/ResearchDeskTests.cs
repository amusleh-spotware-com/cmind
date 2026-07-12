using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core;
using Core.Agent;
using Core.Ai;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class ResearchDeskTests
{
    private static TradingAgent Agent() =>
        TradingAgent.Create(UserId.New(), "Desk", AgentArchetype.SwingTrader, AgentTemperament.Balanced);

    private sealed class ScriptedAiClient(bool enabled, string reviewerJson) : IAiClient
    {
        private int _call;
        public bool Enabled { get; } = enabled;
        public List<string> Systems { get; } = [];

        public Task<AiResult> CompleteAsync(AiTextRequest request, CancellationToken ct)
        {
            Systems.Add(request.System);
            _call++;
            // First four calls are the analysts; the fifth is the reviewer (returns JSON).
            return Task.FromResult(_call <= 4 ? AiResult.Ok($"opinion {_call}") : AiResult.Ok(reviewerJson));
        }
    }

    [Fact]
    public async Task Debate_collects_opinions_and_a_parsed_proposal()
    {
        var ai = new ScriptedAiClient(true, """{ "reasoning": "buy the breakout", "action": "buy", "symbol": "EURUSD", "sizeLots": 1 }""");
        var result = await new ResearchDesk(ai).DebateAsync(Agent(), "context", CancellationToken.None);

        result.Opinions.Should().HaveCount(4);
        result.Opinions[0].Role.Should().Be(AgentRole.Alpha);
        result.Proposal.Side.Should().Be("Buy");
        result.Proposal.Symbol.Should().Be("EURUSD");
        ai.Systems.Should().HaveCount(5); // 4 analysts + 1 reviewer
        ai.Systems[4].Should().Contain("Reviewer");
    }

    [Fact]
    public async Task Debate_is_disabled_without_ai()
    {
        var result = await new ResearchDesk(new ScriptedAiClient(false, "{}")).DebateAsync(Agent(), "context", CancellationToken.None);
        result.Opinions.Should().BeEmpty();
        result.Synthesis.Should().Contain("not configured");
        result.Proposal.Side.Should().BeNull();
    }
}
