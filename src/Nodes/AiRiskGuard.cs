using Core;
using Core.Ai;
using Core.Constants;
using Core.Logging;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nodes;

public sealed class AiRiskGuard(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<AppOptions> options,
    ILogger<AiRiskGuard> logger) : BackgroundService
{
    private const int MaxSummaryChars = 500;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(options.CurrentValue.Ai.RiskGuardInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }

            try { await ScanAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.RiskGuardFailed(ex); }
        }
    }

    private async Task ScanAsync(CancellationToken ct)
    {
        var ai = options.CurrentValue.Ai;
        if (!ai.RiskGuardEnabled) return;

        using var scope = scopeFactory.CreateScope();
        var aiService = scope.ServiceProvider.GetRequiredService<IAiFeatureService>();
        if (!aiService.Enabled) return;

        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var entities = await db.Instances.OfType<RunningRunInstance>()
            .Include(i => i.CBot)
            .Select(i => new { i.Id, CBotName = i.CBot.Name, i.Symbol, i.Timeframe })
            .ToListAsync(ct);
        if (entities.Count == 0) return;

        var contexts = entities
            .Select(i => new AiInstanceContext(i.CBotName, "Run", "Running", i.Symbol, i.Timeframe, null))
            .ToList();

        if (!ai.RiskGuardAutoStop)
        {
            var summary = await aiService.AssessRiskAsync(contexts, ct);
            if (summary.Success)
                logger.RiskGuardAssessment(contexts.Count, Truncate(summary.Text, MaxSummaryChars));
            return;
        }

        var result = await aiService.AssessRiskActionsAsync(contexts, RiskGuardConstants.ActionMaxTokens, ct);
        if (!result.Success) return;

        foreach (var verdict in RiskGuardJson.ParseVerdicts(result.Text))
        {
            if (!RiskGuardJson.WantsStop(verdict)) continue;
            if (verdict.Ref < 0 || verdict.Ref >= entities.Count)
            {
                logger.RiskGuardStopSkipped(verdict.Ref, entities.Count);
                continue;
            }
            await StopInstanceAsync(entities[verdict.Ref].Id, verdict.Reason, ct);
        }
    }

    private async Task StopInstanceAsync(InstanceId id, string reason, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var factory = scope.ServiceProvider.GetRequiredService<IContainerDispatcherFactory>();

            var instance = await db.Instances.OfType<RunningRunInstance>()
                .Include(i => i.Node)
                .FirstOrDefaultAsync(i => i.Id == id, ct);
            if (instance is null) return;

            if (instance.Node is null)
                logger.RiskGuardNodeMissing(id.Value);
            else
                try { await factory.For(instance).StopAsync(instance, ct); }
                catch (Exception ex) when (ex is not OperationCanceledException) { /* best effort */ }

            var terminal = instance.ToStopped(DateTimeOffset.UtcNow);
            db.Instances.Remove(instance);
            db.Instances.Add(terminal);
            db.AuditLogs.Add(AuditLog.Record(
                RiskGuardConstants.AuditAction, RiskGuardConstants.AuditEntityType,
                instance.UserId, id.Value, detailsJson: reason));
            await db.SaveChangesAsync(ct);
            logger.RiskGuardStopped(id.Value, Truncate(reason, MaxSummaryChars));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { logger.RiskGuardFailed(ex); }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
