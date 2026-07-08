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

/// <summary>
/// Reconciles self-registered node liveness: a node whose last heartbeat is older than the
/// configured TTL is flagged unreachable so the scheduler stops placing work on it. A resumed
/// heartbeat (handled by the registration endpoint) brings it back online.
/// </summary>
public sealed class NodeHeartbeatMonitor(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<AppOptions> options,
    ILogger<NodeHeartbeatMonitor> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var discovery = options.CurrentValue.Discovery;
            if (discovery.Enabled)
            {
                try { await SweepAsync(discovery.HeartbeatTtl, stoppingToken); }
                catch (Exception ex) { log.HeartbeatMonitorFailed(ex); }
            }
            await Task.Delay(options.CurrentValue.Discovery.MonitorInterval, stoppingToken);
        }
    }

    private async Task SweepAsync(TimeSpan ttl, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var now = DateTimeOffset.UtcNow;

        // OfType<RemoteNode>() over the soft-delete-filtered TPH set does not translate on Npgsql;
        // enumerate the concrete remote subtypes, then filter reachability in memory.
        var candidates = await db.Nodes
            .Where(n => n is ActiveRunNode || n is ActiveBacktestNode || n is ActiveMixedNode
                        || n is DecommissioningNode || n is OfflineNode)
            .ToListAsync(ct);

        var changed = false;
        foreach (var node in candidates.Cast<RemoteNode>())
        {
            if (!node.IsReachable || node.LastHeartbeatAt is null) continue;
            if (!node.IsHeartbeatStale(ttl, now)) continue;
            node.MarkUnreachable();
            log.NodeMarkedUnreachable(node.Name);
            changed = true;
        }

        if (changed) await db.SaveChangesAsync(ct);
    }
}
