using Core;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Web.Endpoints;

/// <summary>Builds the composite dashboard read model. Kept separate from the endpoint so it can be
/// exercised directly against a real Postgres <see cref="DataContext"/> in integration tests.
/// Read-only: no domain decisions here, only projection and aggregation.</summary>
public static class DashboardQuery
{
    public static async Task<DashboardOverview> BuildAsync(
        DataContext db, UserId userId, bool isAdmin, DashboardPeriod period, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var (window, buckets) = DashboardPeriods.Plan(period);

        // All-time status snapshot — active/pending/etc. are not window-bounded.
        var counts = await db.Instances.Where(i => i.UserId == userId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Running = g.Count(i => i is RunningRunInstance || i is StartingRunInstance),
                BacktestRunning = g.Count(i => i is RunningBacktestInstance || i is StartingBacktestInstance),
                Pending = g.Count(i => i is PendingRunInstance || i is PendingBacktestInstance),
                Failed = g.Count(i => i is FailedRunInstance || i is FailedBacktestInstance),
                Completed = g.Count(i => i is StoppedRunInstance || i is CompletedBacktestInstance),
                Total = g.Count()
            })
            .FirstOrDefaultAsync(cancellationToken);

        var running = counts?.Running ?? 0;
        var backtestRunning = counts?.BacktestRunning ?? 0;
        var activeNow = running + backtestRunning;

        // Window (x2 for previous-period deltas). StatusName/timestamps live on TPH subtypes and are not
        // EF-projectable, so materialize the rows (single TPH table, no navigations/blobs) and read them
        // in memory via the shared column helpers.
        var windowStart = now - window * 2;
        var recent = await db.Instances.AsNoTracking()
            .Where(i => i.UserId == userId && i.CreatedAt >= windowStart)
            .ToListAsync(cancellationToken);

        var events = recent.Select(ToEvent).ToList();
        var kpis = DashboardMath.BuildKpis(events, activeNow, now, window, buckets);
        var timeSeries = DashboardMath.BuildBuckets(events, now, window, buckets);

        var ordered = recent.OrderByDescending(EventTime).Take(20).ToList();
        var cbotIds = ordered.Select(i => i.CBotId).Distinct().ToList();
        var names = await db.CBots.AsNoTracking()
            .Where(c => cbotIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(cancellationToken);
        var nameById = names.ToDictionary(x => x.Id, x => x.Name);

        var activity = ordered.Select(i => new DashboardActivity
        {
            At = EventTime(i),
            Kind = i.KindName,
            Status = i.StatusName,
            Symbol = i.Symbol,
            Timeframe = i.Timeframe,
            CBot = nameById.TryGetValue(i.CBotId, out var name) ? name : "—"
        }).ToList();

        var resources = new DashboardResources
        {
            CBots = await db.CBots.CountAsync(c => c.UserId == userId, cancellationToken),
            ParamSets = await db.ParamSets.CountAsync(p => p.UserId == userId, cancellationToken),
            TradingAccounts = await db.TradingAccounts.CountAsync(t => t.CTid.UserId == userId, cancellationToken),
            Ctids = await db.CTids.CountAsync(c => c.UserId == userId, cancellationToken),
            McpKeys = await db.McpApiKeys.CountAsync(k => k.UserId == userId && k.RevokedAt == null, cancellationToken)
        };

        DashboardNodes? nodes = null;
        if (isAdmin)
        {
            var total = await db.Nodes.CountAsync(cancellationToken);
            var active = await db.Nodes.CountAsync(
                n => n is ActiveRunNode || n is ActiveBacktestNode || n is ActiveMixedNode
                     || (n is LocalNode && ((LocalNode)n).Enabled), cancellationToken);
            var capacityTotal = await db.Nodes.SumAsync(n => n.MaxInstances, cancellationToken);
            nodes = new DashboardNodes
            {
                Total = total,
                Active = active,
                CapacityUsed = activeNow,
                CapacityTotal = capacityTotal
            };
        }

        return new DashboardOverview
        {
            UpdatedAt = now,
            IsAdmin = isAdmin,
            Kpis = kpis,
            Status = new DashboardStatusBreakdown
            {
                Running = running,
                Pending = counts?.Pending ?? 0,
                Failed = counts?.Failed ?? 0,
                Completed = counts?.Completed ?? 0,
                BacktestsRunning = backtestRunning,
                Total = counts?.Total ?? 0
            },
            TimeSeries = timeSeries,
            Activity = activity,
            Resources = resources,
            Nodes = nodes
        };
    }

    private static InstanceEvent ToEvent(Instance instance)
    {
        var stoppedAt = InstanceEndpoints.GetStoppedAt(instance);
        var completed = instance is StoppedRunInstance or CompletedBacktestInstance;
        var failed = instance is FailedRunInstance or FailedBacktestInstance;
        return new InstanceEvent(instance.CreatedAt, completed ? stoppedAt : null, failed ? stoppedAt : null);
    }

    private static DateTimeOffset EventTime(Instance instance) =>
        InstanceEndpoints.GetStoppedAt(instance) ?? InstanceEndpoints.GetStartedAt(instance) ?? instance.CreatedAt;
}
