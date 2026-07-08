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
