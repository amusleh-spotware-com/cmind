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
    IOptionsMonitor<AppOptions> options,
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
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var factory = scope.ServiceProvider.GetRequiredService<IContainerDispatcherFactory>();

        var nodes = await db.Nodes.Where(n =>
            n is ActiveRunNode || n is ActiveBacktestNode || n is ActiveMixedNode
            || (n is LocalNode && ((LocalNode)n).Enabled)).ToListAsync(ct);
        foreach (var node in nodes)
        {
            try
            {
                var stats = await factory.For(node).CollectStatsAsync(node, ct);
                var runningCount = await db.Instances.CountAsync(i =>
                    i.NodeId == node.Id && i is RunningRunInstance, ct);
                var backtestCount = await db.Instances.CountAsync(i =>
                    i.NodeId == node.Id && i is RunningBacktestInstance, ct);
                stats.SetInstanceCounts(runningCount, backtestCount);

                var existing = await db.NodeStats.FindAsync([node.Id], ct);
                if (existing is null) db.NodeStats.Add(stats);
                else db.Entry(existing).CurrentValues.SetValues(stats);
            }
            catch (Exception ex) { log.NodeStatsFailed(node.Name, ex); }
        }
        await db.SaveChangesAsync(ct);
    }
}
