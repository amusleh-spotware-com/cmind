using Core;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nodes;

public sealed class NodeScheduler(DataContext db) : INodeScheduler
{
    public async Task<Node?> PickNodeAsync(string kind, CancellationToken ct)
    {
        var wantRun = string.Equals(kind, "Run", StringComparison.OrdinalIgnoreCase);

        var candidates = await db.Nodes
            .Where(n => n is ActiveMixedNode
                        || (wantRun ? n is ActiveRunNode : n is ActiveBacktestNode))
            .Include(n => n.LatestStats)
            .ToListAsync(ct);

        var activeCounts = await db.Instances
            .Where(i => i.NodeId != null && (
                i is RunningRunInstance || i is StartingRunInstance || i is ScheduledRunInstance ||
                i is RunningBacktestInstance || i is StartingBacktestInstance || i is ScheduledBacktestInstance))
            .GroupBy(i => i.NodeId!.Value)
            .Select(g => new { NodeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.NodeId, x => x.Count, ct);

        return candidates
            .Where(n => activeCounts.GetValueOrDefault(n.Id) < n.MaxInstances)
            .OrderBy(n => activeCounts.GetValueOrDefault(n.Id))
            .ThenBy(n => n.LatestStats?.CpuPercent ?? 0)
            .FirstOrDefault();
    }
}
