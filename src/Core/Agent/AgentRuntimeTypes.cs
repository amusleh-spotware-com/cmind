using Core.Execution;

namespace Core.Agent;

/// <summary>
/// A point-in-time snapshot of one managed account, sourced from the deterministic account-state store
/// (never from the LLM's memory). The agent reads this; it is the ground truth every decision is
/// validated against.
/// </summary>
public sealed record AccountState(
    TradingAccountId Account,
    decimal Balance,
    decimal Equity,
    double OpenExposureLots,
    int ConsecutiveLosses,
    double DailyLossPercent,
    int OrdersThisHour);

/// <summary>An order the agent proposes to place, targeting one managed account.</summary>
public sealed record AgentOrderIntent(TradingAccountId Account, string Symbol, OrderSide Side, double SizeLots);

/// <summary>
/// The decision engine's output: the reasoning (XAI), an optional order (null = hold), and the evidence
/// (ids of backtests/signals) the reasoning rests on. Deterministically validated before it can act.
/// </summary>
public sealed record AgentDecision(string Reasoning, AgentOrderIntent? Order, IReadOnlyList<string> Evidence);

/// <summary>What actually happened to a decision after the safety gate.</summary>
public enum DecisionOutcome
{
    /// <summary>No order, or advisory/approval-gated — recorded as a proposal, not executed.</summary>
    Held,

    /// <summary>Passed every check and is cleared to execute.</summary>
    Cleared,

    /// <summary>An order that would breach the risk envelope — refused.</summary>
    RejectedByEnvelope,

    /// <summary>The circuit breaker tripped — the agent must halt new entries.</summary>
    HaltedByBreaker
}

/// <summary>The safety gate's verdict on a proposed decision.</summary>
public sealed record ProcessedDecision(
    DecisionOutcome Outcome,
    string Reason,
    bool ShouldExecute,
    bool ShouldHalt,
    AgentOrderIntent? Order);
