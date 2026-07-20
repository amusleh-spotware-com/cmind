using Core;

namespace Core.Agent;

public enum AgentAutonomy
{
    Suggest = 0,
    Approve = 1,
    Auto = 2
}

public enum AgentProposalStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Executed = 3,
    Failed = 4
}

public sealed record AgentAction(string Reasoning, string Name, string ParametersJson);

public interface IAgentExecutor
{
    Task<bool> ExecuteAsync(AgentProposalId proposalId, UserId actor, CancellationToken ct);
}

/// <summary>
/// Runs a single agent-mandate cycle on demand — the same work the scheduled <c>PortfolioAgentService</c>
/// does per due mandate: ask the model for an action, record the run, and (in Auto) execute the proposal.
/// Used by the background scheduler and by the "Run now" endpoint (fire-and-forget).
/// </summary>
public interface IAgentMandateRunner
{
    Task RunOnceAsync(AgentMandateId mandateId, UserId actor, CancellationToken ct);
}
