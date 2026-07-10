using Core;
using Core.Constants;
using Core.Logging;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nodes.PropGuard;

public sealed record PropRuleSnapshot(PropRuleId Id, UserId UserId, TradingAccountId TradingAccountId, int MaxConcurrentLiveInstances);

public sealed class PropGuardService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<AppOptions> options,
    ILogger<PropGuardService> logger,
    TimeProvider timeProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(options.CurrentValue.PropGuard.PollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }

            try { await ScanAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.PropGuardCycleFailed(ex); }
        }
    }

    private async Task ScanAsync(CancellationToken ct)
    {
        if (!options.CurrentValue.PropGuard.Enabled) return;

        List<PropRuleSnapshot> rules;
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            rules = await db.PropRules
                .Where(r => r.Enabled && r.AutoFlatten)
                .Select(r => new PropRuleSnapshot(r.Id, r.UserId, r.TradingAccountId, r.MaxConcurrentLiveInstances))
                .Take(PropGuardConstants.MaxRulesPerCycle)
                .ToListAsync(ct);
        }

        foreach (var rule in rules)
        {
            ct.ThrowIfCancellationRequested();
            try { await EnforceAsync(rule, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { logger.PropGuardRuleFailed(rule.Id.Value, ex); }
        }
    }

    private async Task EnforceAsync(PropRuleSnapshot rule, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var userId = rule.UserId;
        var accountId = rule.TradingAccountId;
        var liveCount = await db.Instances.OfType<RunningRunInstance>()
            .CountAsync(i => i.UserId == userId && i.TradingAccountId == accountId, ct);
        if (liveCount <= rule.MaxConcurrentLiveInstances) return;

        var factory = scope.ServiceProvider.GetRequiredService<IContainerDispatcherFactory>();
        await FlattenAccountAsync(db, factory, userId, accountId, ct);
    }

    private async Task FlattenAccountAsync(
        DataContext db, IContainerDispatcherFactory factory, UserId userId, TradingAccountId accountId, CancellationToken ct)
    {
        var live = await db.Instances.OfType<RunningRunInstance>()
            .Include(i => i.Node)
            .Where(i => i.UserId == userId && i.TradingAccountId == accountId)
            .ToListAsync(ct);
        if (live.Count == 0) return;

        var now = timeProvider.GetUtcNow();
        foreach (var instance in live)
        {
            if (instance.Node is not null)
                try { await factory.For(instance).StopAsync(instance, ct); }
                catch (Exception ex) when (ex is not OperationCanceledException) { /* best effort */ }

            db.Instances.Remove(instance);
            db.Instances.Add(InstanceTransitions.StoppedFrom(instance, now));
        }

        db.AuditLogs.Add(AuditLog.Record(
            PropGuardConstants.AuditAction, PropGuardConstants.AuditEntityType,
            now, live[0].UserId, accountId.Value, detailsJson: $"{{\"flattened\":{live.Count}}}"));
        await db.SaveChangesAsync(ct);
        logger.PropGuardFlattened(accountId.Value, live.Count);
    }
}
