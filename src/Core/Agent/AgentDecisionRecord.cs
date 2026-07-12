using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Core.Constants;
using Core.Domain;

namespace Core.Agent;

/// <summary>The human-in-the-loop approval state of a proposed order.</summary>
public enum DecisionApproval
{
    /// <summary>No approval needed (a hold, an executed Full-Auto order, or a rejected one).</summary>
    NotRequired,

    /// <summary>An approval-gated order awaiting the owner's decision.</summary>
    Pending,

    /// <summary>The owner approved the order; it is cleared to execute.</summary>
    Approved,

    /// <summary>The owner rejected the order.</summary>
    Rejected
}

/// <summary>
/// An append-only record of one autonomous decision: the reasoning (XAI), the evidence it cited, the
/// safety-gate verdict, and the resulting order intent — everything needed to replay <em>why</em> a
/// trade happened against the point-in-time state the agent saw. The audit ledger for Full Auto.
/// </summary>
public sealed class AgentDecisionRecord : AuditedEntity<AgentDecisionRecordId>
{
    public TradingAgentId AgentId { get; private set; }
    public UserId UserId { get; private set; }
    public long Sequence { get; private set; }

    [MaxLength(24)] public string Outcome { get; private set; } = default!;
    [MaxLength(2000)] public string Reasoning { get; private set; } = default!;
    [MaxLength(512)] public string? Reason { get; private set; }
    [MaxLength(1024)] public string? OrderJson { get; private set; }
    [MaxLength(1024)] public string EvidenceCsv { get; private set; } = string.Empty;
    public bool ShouldExecute { get; private set; }
    public bool ShouldHalt { get; private set; }
    public bool Executed { get; private set; }
    public DecisionApproval Approval { get; private set; }

    private AgentDecisionRecord()
    {
    }

    public static AgentDecisionRecord Create(
        TradingAgentId agentId, UserId userId, long sequence, AgentDecision decision, ProcessedDecision processed)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(processed);
        return new AgentDecisionRecord
        {
            AgentId = agentId,
            UserId = userId,
            Sequence = sequence,
            Outcome = processed.Outcome.ToString(),
            Reasoning = Clip(decision.Reasoning, 2000),
            Reason = processed.Reason is { } r ? Clip(r, 512) : null,
            OrderJson = processed.Order is { } o ? JsonSerializer.Serialize(o) : null,
            EvidenceCsv = decision.Evidence.Count > 0 ? Clip(string.Join(',', decision.Evidence), 1024) : string.Empty,
            ShouldExecute = processed.ShouldExecute,
            ShouldHalt = processed.ShouldHalt,
            Approval = processed.Outcome == DecisionOutcome.PendingApproval ? DecisionApproval.Pending : DecisionApproval.NotRequired,
        };
    }

    /// <summary>Marks that the cleared order actually executed on the live account.</summary>
    public void MarkExecuted() => Executed = true;

    /// <summary>Owner approves a pending order — it becomes cleared to execute on the next runtime tick.</summary>
    public void Approve()
    {
        if (Approval != DecisionApproval.Pending) throw new DomainException(DomainErrors.AgentTransitionInvalid);
        Approval = DecisionApproval.Approved;
        ShouldExecute = true;
    }

    /// <summary>Owner rejects a pending order — it will never act.</summary>
    public void Reject()
    {
        if (Approval != DecisionApproval.Pending) throw new DomainException(DomainErrors.AgentTransitionInvalid);
        Approval = DecisionApproval.Rejected;
    }

    private static string Clip(string value, int max) => value.Length <= max ? value : value[..max];
}
