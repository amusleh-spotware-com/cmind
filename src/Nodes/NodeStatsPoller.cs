using Core;
using Core.Logging;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nodes;

public sealed class NodeStatsPoller(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<CtwOptions> options,
    ILogger<NodeStatsPoller> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PollOnceAsync(stoppingToken); }
            catch (Exception ex) { log.StatsPollFailed(ex); }
            await Task.Delay(options.CurrentValue.NodeStatsPollInterval, stoppingToken);
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CtwDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IContainerDispatcher>();

        var active = NodeStatus.Active;
        var running = InstanceStatus.Running;
        var runType = InstanceType.Run;
        var backtestType = InstanceType.Backtest;

        var nodes = await db.Nodes.Where(n => n.Status == active).ToListAsync(ct);
        foreach (var node in nodes)
        {
            try
            {
                var stats = await dispatcher.CollectStatsAsync(node, ct);
                stats.RunningCount = await db.Instances.CountAsync(i =>
                    i.NodeId == node.Id && i.Type == runType && i.Status == running, ct);
                stats.BacktestCount = await db.Instances.CountAsync(i =>
                    i.NodeId == node.Id && i.Type == backtestType && i.Status == running, ct);

                var existing = await db.NodeStats.FindAsync([node.Id], ct);
                if (existing is null) db.NodeStats.Add(stats);
                else db.Entry(existing).CurrentValues.SetValues(stats);
            }
            catch (Exception ex) { log.NodeStatsFailed(node.Name, ex); }
        }
        await db.SaveChangesAsync(ct);
    }
}
