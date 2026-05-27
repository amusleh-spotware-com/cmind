using Core;
using Core.Logging;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Web.Auth;

public sealed class InstanceReconciler : BackgroundService
{
    private readonly IServiceScopeFactory _sf;
    private readonly ILogger<InstanceReconciler> _log;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    public InstanceReconciler(IServiceScopeFactory sf, ILogger<InstanceReconciler> log)
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
                using var scope = _sf.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CtwDbContext>();
                var stale = await db.Instances
                    .Where(i => (i.Status == InstanceStatus.Starting || i.Status == InstanceStatus.Pending)
                                && i.CreatedAt < DateTimeOffset.UtcNow.AddMinutes(-10))
                    .ToListAsync(stoppingToken);
                foreach (var i in stale)
                {
                    i.Status = InstanceStatus.Failed;
                    i.FailureReason = "Reconcile timeout";
                }
                if (stale.Count > 0) await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _log.ReconcileFailed(ex);
            }
            await Task.Delay(Interval, stoppingToken);
        }
    }
}
