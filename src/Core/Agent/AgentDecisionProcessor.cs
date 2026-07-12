using Core.Autonomy;

namespace Core.Agent;

/// <summary>
/// The deterministic safety gate that every proposed agent decision must pass before it can touch a live
/// account. It applies, in order: the autonomy level (only Full Auto may auto-execute), the circuit
/// breaker (loss streak / daily loss / hard-goal breach / AI unavailable), and the risk envelope
/// (per-order limits). No LLM here — this is the anti-hallucination backstop around the LLM.
/// </summary>
public interface IAgentDecisionProcessor
{
    ProcessedDecision Process(TradingAgent agent, AccountState state, AgentDecision decision, bool aiAvailable, bool hardGoalBreached);
}

public sealed class AgentDecisionProcessor : IAgentDecisionProcessor
{
    public ProcessedDecision Process(TradingAgent agent, AccountState state, AgentDecision decision, bool aiAvailable, bool hardGoalBreached)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(decision);

        // Circuit breaker runs regardless of autonomy so a running agent halts even mid-advice. It needs
        // an envelope; for non-Full-Auto agents (which may have none) fall back to a permissive envelope
        // that still honours the AI-availability and hard-goal signals.
        var envelope = agent.Envelope;
        if (envelope is not null)
        {
            var breaker = CircuitBreaker.Evaluate(envelope,
                new BreakerMetrics(state.ConsecutiveLosses, state.DailyLossPercent, aiAvailable, hardGoalBreached));
            if (breaker.Tripped)
                return new ProcessedDecision(DecisionOutcome.HaltedByBreaker, breaker.Reason ?? "halted", false, true, null);
        }
        else if (!aiAvailable || hardGoalBreached)
        {
            return new ProcessedDecision(DecisionOutcome.HaltedByBreaker,
                !aiAvailable ? "AI provider unavailable." : "A hard performance goal was breached.", false, true, null);
        }

        // No order → a hold, whatever the autonomy level.
        if (decision.Order is null)
            return new ProcessedDecision(DecisionOutcome.Held, "No order proposed.", false, false, null);

        // Advisory proposes only; approval-gated proposes and waits for the owner; only Full Auto auto-executes.
        if (agent.Autonomy == AutonomyLevel.Advisory)
            return new ProcessedDecision(DecisionOutcome.Held, "Advisory — recorded as a proposal.", false, false, decision.Order);
        if (agent.Autonomy == AutonomyLevel.ApprovalGated)
            return new ProcessedDecision(DecisionOutcome.PendingApproval, "Awaiting owner approval before this order can act.", false, false, decision.Order);

        // Full Auto with an order → validate against the hard risk envelope (guaranteed present here).
        var order = decision.Order;
        var check = envelope!.CheckOrder(order.Symbol, order.SizeLots, state.OpenExposureLots, state.OrdersThisHour);
        return check.Allowed
            ? new ProcessedDecision(DecisionOutcome.Cleared, "Cleared by the risk envelope.", true, false, order)
            : new ProcessedDecision(DecisionOutcome.RejectedByEnvelope, check.Reason ?? "rejected", false, false, order);
    }
}
