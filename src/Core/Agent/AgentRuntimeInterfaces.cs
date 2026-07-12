namespace Core.Agent;

/// <summary>
/// The deterministic, read-only source of an account's live state (positions, balance, exposure). Ground
/// truth for the agent — never the LLM's memory. Implemented over the cTrader Open API; a degraded
/// implementation returns a safe empty state when no live connection is configured.
/// </summary>
public interface IAccountStateStore
{
    Task<AccountState> GetStateAsync(TradingAccountId account, CancellationToken ct);
}

/// <summary>
/// Produces an agent's next decision from its persona and the current observation. Backed by the LLM; a
/// degraded implementation returns a Hold when AI is not configured, so the agent never acts blind.
/// </summary>
public interface IAgentDecisionEngine
{
    Task<AgentDecision> DecideAsync(TradingAgent agent, AccountState state, CancellationToken ct);
}

/// <summary>
/// Places a cleared order on the live account. Implemented over the cTrader Open API order path; a
/// degraded implementation reports "no live connection" so nothing is silently dropped.
/// </summary>
public interface IAgentOrderExecutor
{
    Task<bool> ExecuteAsync(TradingAgent agent, AgentOrderIntent order, CancellationToken ct);
}
