using Core;
using Core.Agent;
using Core.Ai;

namespace Infrastructure.Agent;

/// <summary>
/// Degraded account-state store used when no live cTrader Open API connection is configured: returns a
/// safe, flat state so the runtime and safety gate operate deterministically without ever inventing data.
/// The live Open-API-backed implementation supersedes this when credentials are present.
/// </summary>
public sealed class NullAccountStateStore : IAccountStateStore
{
    public Task<AccountState> GetStateAsync(TradingAccountId account, CancellationToken ct) =>
        Task.FromResult(new AccountState(account, Balance: 0m, Equity: 0m, OpenExposureLots: 0,
            ConsecutiveLosses: 0, DailyLossPercent: 0, OrdersThisHour: 0));
}

/// <summary>
/// Degraded executor used when there is no live order path: it never places an order and reports that
/// nothing was executed, so a cleared order is recorded but not silently assumed filled.
/// </summary>
public sealed class NullAgentOrderExecutor : IAgentOrderExecutor
{
    public Task<bool> ExecuteAsync(TradingAgent agent, AgentOrderIntent order, CancellationToken ct) =>
        Task.FromResult(false);
}

/// <summary>
/// LLM-backed decision engine. When AI is configured it asks the model for the agent's next move (using
/// the deterministic persona prompt) and returns the reasoning; when AI is unavailable it returns a Hold
/// so the agent never acts blind. Automatic order extraction from the model is intentionally deferred
/// until the API key is provided — the safety gate and execution wiring are exercised deterministically.
/// </summary>
public sealed class AiAgentDecisionEngine(IAiFeatureService ai, IAgentMemory memory) : IAgentDecisionEngine
{
    private const int MaxTokens = 1024;
    private const int RecallCount = 3;

    public async Task<AgentDecision> DecideAsync(TradingAgent agent, AccountState state, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(agent);
        if (!ai.Enabled)
            return new AgentDecision("AI is not configured — holding.", Order: null, Evidence: []);

        // Recall recent memory so the model reasons with continuity (layered memory / reflection).
        var recent = await memory.RecallAsync(agent.Id, RecallCount, ct);
        var objective = recent.Count == 0
            ? agent.CompileSystemPrompt()
            : agent.CompileSystemPrompt() + "\nRecent memory:\n" + string.Join("\n", recent.Select(m => $"- {m.Content}"));
        var result = await ai.ProposeAgentActionAsync(agent.Name, objective, "{}", null, MaxTokens, ct);
        if (result is not { Success: true })
            return new AgentDecision(result.Error ?? "AI returned no decision.", Order: null, Evidence: []);

        // Deterministically parse the model reply into an order for this account; anything malformed holds.
        var parsed = AgentDecisionParser.Parse(result.Text);
        return AgentDecisionParser.ToDecision(parsed, state.Account);
    }
}
