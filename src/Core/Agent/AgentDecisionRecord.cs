using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Core.Domain;

namespace Core.Agent;

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
        };
    }

    /// <summary>Marks that the cleared order actually executed on the live account.</summary>
    public void MarkExecuted() => Executed = true;

    private static string Clip(string value, int max) => value.Length <= max ? value : value[..max];
}
