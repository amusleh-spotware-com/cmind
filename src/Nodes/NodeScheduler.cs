using Core;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nodes;

public sealed class NodeScheduler(CtwDbContext db) : INodeScheduler
{
    public async Task<Node?> PickNodeAsync(InstanceType type, CancellationToken ct)
    {
        var allowed = type switch
        {
            InstanceType.RunType => new[] { NodeMode.Run, NodeMode.Mixed },
            InstanceType.BacktestType => new[] { NodeMode.Backtest, NodeMode.Mixed },
            _ => new[] { NodeMode.Mixed }
        };
        var active = NodeStatus.Active;
        var modeA = allowed[0];
        var modeB = allowed.Length > 1 ? allowed[1] : allowed[0];

        var candidates = await db.Nodes
            .Where(n => (n.Mode == modeA || n.Mode == modeB) && n.Status == active)
            .Include(n => n.LatestStats)
            .ToListAsync(ct);

        var running = InstanceStatus.Running;
        var starting = InstanceStatus.Starting;
        var scheduled = InstanceStatus.Scheduled;

        var activeCounts = await db.Instances
            .Where(i => i.NodeId != null &&
                        (i.Status == running || i.Status == starting || i.Status == scheduled))
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
