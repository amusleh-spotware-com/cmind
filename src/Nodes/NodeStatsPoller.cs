using Core;
using Core.Logging;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Nodes;

public sealed class NodeStatsPoller : BackgroundService
{
    private readonly IServiceScopeFactory _sf;
    private readonly ILogger<NodeStatsPoller> _log;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    public NodeStatsPoller(IServiceScopeFactory sf, ILogger<NodeStatsPoller> log)
    {
        _sf = sf;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _log.StatsPollFailed(ex);
            }
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        using var scope = _sf.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CtwDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IContainerDispatcher>();

        var nodes = await db.Nodes.Where(n => n.Status == NodeStatus.Active).ToListAsync(ct);
        foreach (var node in nodes)
        {
            try
            {
                var stats = await dispatcher.CollectStatsAsync(node, ct);
                stats.RunningCount = await db.Instances.CountAsync(i =>
                    i.NodeId == node.Id && i.Type == InstanceType.Run && i.Status == InstanceStatus.Running, ct);
                stats.BacktestCount = await db.Instances.CountAsync(i =>
                    i.NodeId == node.Id && i.Type == InstanceType.Backtest && i.Status == InstanceStatus.Running, ct);

                var existing = await db.NodeStats.FindAsync([node.Id], ct);
                if (existing is null) db.NodeStats.Add(stats);
                else db.Entry(existing).CurrentValues.SetValues(stats);
            }
            catch (Exception ex)
            {
                _log.NodeStatsFailed(node.Name, ex);
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
