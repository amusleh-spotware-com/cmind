using Core;
using Core.Agent;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Agent;

/// <summary>
/// EF-backed agent memory: appends observations/reflections and recalls the most recent by recency.
/// Embedding-based similarity retrieval is the enhancement layered on when the AI key is present.
/// </summary>
public sealed class EfAgentMemory(DataContext db) : IAgentMemory
{
    public async Task RememberAsync(TradingAgentId agentId, UserId userId, MemoryTier tier, string content, CancellationToken ct)
    {
        db.AgentMemories.Add(AgentMemoryRecord.Create(agentId, userId, tier, content));
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AgentMemoryRecord>> RecallAsync(TradingAgentId agentId, int limit, CancellationToken ct) =>
        await db.AgentMemories
            .Where(m => m.AgentId == agentId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(Math.Clamp(limit, 1, 50))
            .ToListAsync(ct);
}
