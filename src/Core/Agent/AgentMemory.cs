using System.ComponentModel.DataAnnotations;
using Core.Constants;
using Core.Domain;

namespace Core.Agent;

/// <summary>The tier of an agent memory, mirroring layered human cognition (market facts → reflections).</summary>
public enum MemoryTier
{
    /// <summary>Raw market intelligence the agent observed.</summary>
    MarketIntelligence,

    /// <summary>Short-horizon reflection on a recent decision or signal.</summary>
    LowLevelReflection,

    /// <summary>Aggregated, longer-horizon lessons.</summary>
    HighLevelReflection
}

/// <summary>An append-only memory an agent can recall to reason with continuity and learn from mistakes.</summary>
public sealed class AgentMemoryRecord : AuditedEntity<AgentMemoryRecordId>
{
    public TradingAgentId AgentId { get; private set; }
    public UserId UserId { get; private set; }
    public MemoryTier Tier { get; private set; }
    [MaxLength(2000)] public string Content { get; private set; } = default!;

    private AgentMemoryRecord()
    {
    }

    public static AgentMemoryRecord Create(TradingAgentId agentId, UserId userId, MemoryTier tier, string content) =>
        new()
        {
            AgentId = agentId,
            UserId = userId,
            Tier = tier,
            Content = Clip(DomainGuard.AgainstNullOrWhiteSpace(content, DomainErrors.AgentMemoryContentRequired), 2000),
        };

    private static string Clip(string value, int max) => value.Length <= max ? value : value[..max];
}

/// <summary>
/// The agent's persistent memory: remember observations and reflections, recall the most recent to give
/// the model continuity. A deterministic recency store today; embedding-based similarity retrieval is the
/// enhancement that arrives with the AI key.
/// </summary>
public interface IAgentMemory
{
    Task RememberAsync(TradingAgentId agentId, UserId userId, MemoryTier tier, string content, CancellationToken ct);
    Task<IReadOnlyList<AgentMemoryRecord>> RecallAsync(TradingAgentId agentId, int limit, CancellationToken ct);
}
