using Core;
using Core.Logging;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Web.Auth;

public sealed class InstanceReconciler(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<AppOptions> options,
    ILogger<InstanceReconciler> log) : BackgroundService
{
    private const string ReconcileTimeoutReason = "Reconcile timeout";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DataContext>();
                var cutoff = DateTimeOffset.UtcNow - options.CurrentValue.InstanceStartupTimeout;
                var stale = await db.Instances
                    .Where(i => (i is PendingRunInstance || i is StartingRunInstance
                                 || i is PendingBacktestInstance || i is StartingBacktestInstance)
                                && i.CreatedAt < cutoff)
                    .ToListAsync(stoppingToken);
                foreach (var i in stale)
                {
                    Instance replacement = i is RunInstance
                        ? new FailedRunInstance
                        {
                            ContainerId = (i as StartingRunInstance)?.ContainerId,
                            FailureReason = ReconcileTimeoutReason
                        }
                        : new FailedBacktestInstance
                        {
                            ContainerId = (i as StartingBacktestInstance)?.ContainerId,
                            FailureReason = ReconcileTimeoutReason
                        };
                    CopyCommon(i, replacement);
                    db.Instances.Remove(i);
                    db.Instances.Add(replacement);
                }
                if (stale.Count > 0) await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex) { log.ReconcileFailed(ex); }
            await Task.Delay(options.CurrentValue.InstanceReconcileInterval, stoppingToken);
        }
    }

    private static void CopyCommon(Instance src, Instance dst)
    {
        dst.Id = src.Id;
        dst.UserId = src.UserId;
        dst.CBotId = src.CBotId;
        dst.TradingAccountId = src.TradingAccountId;
        dst.NodeId = src.NodeId;
        dst.DockerImageTag = src.DockerImageTag;
        dst.Symbol = src.Symbol;
        dst.Timeframe = src.Timeframe;
        dst.ParamSetId = src.ParamSetId;
        dst.DataDirSubPath = src.DataDirSubPath;
        dst.CreatedAt = src.CreatedAt;
    }
}
