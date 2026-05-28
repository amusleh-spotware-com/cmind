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
    IOptionsMonitor<CtwOptions> options,
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
                var db = scope.ServiceProvider.GetRequiredService<CtwDbContext>();
                var starting = InstanceStatus.Starting;
                var pending = InstanceStatus.Pending;
                var cutoff = DateTimeOffset.UtcNow - options.CurrentValue.InstanceStartupTimeout;
                var stale = await db.Instances
                    .Where(i => (i.Status == starting || i.Status == pending)
                                && i.CreatedAt < cutoff)
                    .ToListAsync(stoppingToken);
                foreach (var i in stale)
                {
                    i.Status = InstanceStatus.Failed;
                    i.FailureReason = ReconcileTimeoutReason;
                }
                if (stale.Count > 0) await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex) { log.ReconcileFailed(ex); }
            await Task.Delay(options.CurrentValue.InstanceReconcileInterval, stoppingToken);
        }
    }
}
