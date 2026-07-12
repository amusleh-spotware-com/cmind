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
/// Reclaims non-terminal instances stranded on a node that has gone unreachable. A crashed or
/// partitioned node's containers are unrecoverable from here, so each such instance is transitioned
/// to <c>Failed</c> with a clear reason (instead of being left "Running" forever). Runs are not
/// auto-rescheduled: a partitioned-but-alive node could still be executing the container, and there
/// is no container-level fencing, so silently re-launching would risk double execution. The user
/// restarts a reclaimed run deliberately.
/// </summary>
public sealed class NodeInstanceReclaimer(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<AppOptions> options,
    ILogger<NodeInstanceReclaimer> log,
    TimeProvider timeProvider) : BackgroundService
{
    internal const string NodeUnreachableReason = "Node unreachable - instance reclaimed";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var discovery = options.CurrentValue.Discovery;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DataContext>();
                var staleThreshold = discovery.HeartbeatTtl + discovery.InstanceReclaimGrace;
                await ReclaimAsync(db, staleThreshold, timeProvider.GetUtcNow(), log, stoppingToken);
            }
            catch (Exception ex)
            {
                log.InstanceReclaimFailed(ex);
            }

            await Task.Delay(discovery.MonitorInterval, stoppingToken);
        }
    }

    // A non-terminal instance whose assigned node is a remote node that is currently unreachable and
    // has been heartbeat-stale beyond the threshold. Pure so the decision is unit-tested without a DB.
    internal static bool ShouldReclaim(Instance instance, TimeSpan staleThreshold, DateTimeOffset now)
        => !instance.IsTerminal
           && instance.Node is CtraderCliNode { IsReachable: false } node
           && node.IsHeartbeatStale(staleThreshold, now);

    // Transitions every reclaimable instance to Failed. One SaveChanges for the batch; a concurrent
    // transition elsewhere (e.g. the stop endpoint) surfaces as a concurrency conflict and is retried
    // on the next cycle. Returns the number of instances reclaimed.
    internal static async Task<int> ReclaimAsync(
        DataContext db, TimeSpan staleThreshold, DateTimeOffset now, ILogger log, CancellationToken ct)
    {
        var candidates = await db.Instances
            .Where(i => !(i is StoppedRunInstance || i is FailedRunInstance
                          || i is CompletedBacktestInstance || i is FailedBacktestInstance))
            .Include(i => i.Node)
            .ToListAsync(ct);

        var reclaimed = new List<(Guid InstanceId, string NodeName)>();
        foreach (var instance in candidates)
        {
            if (!ShouldReclaim(instance, staleThreshold, now)) continue;

            var nodeName = (instance.Node as CtraderCliNode)?.Name ?? string.Empty;
            Instance failed = instance switch
            {
                RunInstance run => run.ToFailed(NodeUnreachableReason, now),
                BacktestInstance backtest => backtest.ToFailed(NodeUnreachableReason, now),
                _ => throw new InvalidOperationException($"Unknown instance kind: {instance.GetType().Name}")
            };
            db.Instances.Remove(instance);
            db.Instances.Add(failed);
            reclaimed.Add((instance.Id.Value, nodeName));
        }

        if (reclaimed.Count == 0) return 0;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return 0;
        }

        foreach (var (instanceId, nodeName) in reclaimed)
            log.InstanceReclaimedFromDeadNode(instanceId, nodeName);
        return reclaimed.Count;
    }
}
