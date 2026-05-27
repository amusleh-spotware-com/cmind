using Core;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nodes;

public sealed class NodeScheduler : INodeScheduler
{
    private readonly CtwDbContext _db;
    public NodeScheduler(CtwDbContext db) => _db = db;

    public async Task<Node?> PickNodeAsync(InstanceType type, CancellationToken ct)
    {
        var allowed = type switch
        {
            InstanceType.Run => new[] { NodeMode.Run, NodeMode.Mixed },
            InstanceType.Backtest => new[] { NodeMode.Backtest, NodeMode.Mixed },
            _ => new[] { NodeMode.Mixed }
        };

        var candidates = await _db.Nodes
            .Where(n => n.Status == NodeStatus.Active && allowed.Contains(n.Mode))
            .Include(n => n.LatestStats)
            .ToListAsync(ct);

        var activeCounts = await _db.Instances
            .Where(i => i.NodeId != null &&
                        (i.Status == InstanceStatus.Running ||
                         i.Status == InstanceStatus.Starting ||
                         i.Status == InstanceStatus.Scheduled))
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
