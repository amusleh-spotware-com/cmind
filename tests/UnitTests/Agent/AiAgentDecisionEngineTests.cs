using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core;
using Core.Agent;
using Core.Ai;
using FluentAssertions;
using Infrastructure.Agent;
using Xunit;

namespace UnitTests;

public class AiAgentDecisionEngineTests
{
    private static readonly TradingAccountId Account = TradingAccountId.New();

    private static TradingAgent Agent()
    {
        var a = TradingAgent.Create(UserId.New(), "Auto", AgentArchetype.Scalper, AgentTemperament.Balanced);
        a.SetManagedAccounts([Account]);
        return a;
    }

    private static AccountState State() => new(Account, 10_000m, 10_000m, 0, 0, 0, 0);

    private sealed class FakeAi(bool enabled, string reply) : IAiClient
    {
        public bool Enabled { get; } = enabled;
        public string? LastSystem { get; private set; }
        public Task<AiResult> CompleteAsync(AiTextRequest request, CancellationToken ct)
        {
            LastSystem = request.System;
            return Task.FromResult(AiResult.Ok(reply));
        }
    }

    private sealed class EmptyMemory : IAgentMemory
    {
        public Task RememberAsync(TradingAgentId a, UserId u, MemoryTier t, string c, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<AgentMemoryRecord>> RecallAsync(TradingAgentId a, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AgentMemoryRecord>>([]);
    }

    [Fact]
    public async Task Disabled_ai_holds()
    {
        var engine = new AiAgentDecisionEngine(new FakeAi(false, ""), new EmptyMemory());
        var decision = await engine.DecideAsync(Agent(), State(), CancellationToken.None);
        decision.Order.Should().BeNull();
    }

    [Fact]
    public async Task Json_reply_becomes_an_order_and_prompt_requests_json()
    {
        var fake = new FakeAi(true, """{ "reasoning": "go", "action": "buy", "symbol": "EURUSD", "sizeLots": 1 }""");
        var engine = new AiAgentDecisionEngine(fake, new EmptyMemory());

        var decision = await engine.DecideAsync(Agent(), State(), CancellationToken.None);

        decision.Order.Should().NotBeNull();
        decision.Order!.Symbol.Should().Be("EURUSD");
        decision.Order.Account.Should().Be(Account);
        fake.LastSystem.Should().Contain("JSON").And.Contain("sizeLots");
    }

    [Fact]
    public async Task Malformed_reply_holds()
    {
        var engine = new AiAgentDecisionEngine(new FakeAi(true, "I would buy but not sure"), new EmptyMemory());
        (await engine.DecideAsync(Agent(), State(), CancellationToken.None)).Order.Should().BeNull();
    }
}
