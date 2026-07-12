using Core;
using Core.Ai;
using Core.Constants;
using Core.Domain;
using Core.Logging;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nodes.Alerts;

public sealed class AlertEvaluator(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<AppOptions> options,
    ILogger<AlertEvaluator> logger,
    TimeProvider timeProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(options.CurrentValue.Alerts.PollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }

            try { await ScanAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.AlertCycleFailed(ex); }
        }
    }

    private async Task ScanAsync(CancellationToken ct)
    {
        var config = options.CurrentValue.Alerts;
        if (!config.Enabled) return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var ai = scope.ServiceProvider.GetRequiredService<IAiFeatureService>();
        var now = timeProvider.GetUtcNow();

        // Coarse, translatable SQL prefilter at the minimum interval (a superset of what's due),
        // then exact per-rule interval check in memory (EF/Npgsql can't translate AddMinutes on a column).
        var coarseCutoff = now.AddMinutes(-AlertConstants.MinIntervalMinutes);
        var candidates = await db.AlertRules
            .Where(r => r.Enabled && (r.LastEvaluatedAt == null || r.LastEvaluatedAt < coarseCutoff))
            .OrderBy(r => r.LastEvaluatedAt)
            .Select(r => new { r.Id, r.Symbol, r.IntervalMinutes, r.LastEvaluatedAt, r.Trigger })
            .Take(config.MaxRulesPerCycle * 4)
            .ToListAsync(ct);

        var due = candidates
            .Where(r => r.LastEvaluatedAt == null || r.LastEvaluatedAt < now.AddMinutes(-r.IntervalMinutes))
            // Market-watch rules need AI; when it is off, leave them un-selected (not marked evaluated) so
            // they are picked up once AI returns and never crowd the per-cycle budget out of economic rules.
            .Where(r => r.Trigger == AlertTriggerKind.EconomicEvent || ai.Enabled)
            .Take(config.MaxRulesPerCycle)
            .ToList();

        foreach (var rule in due)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (rule.Trigger == AlertTriggerKind.EconomicEvent)
                    await EvaluateEconomicAsync(rule.Id, ct);
                else
                    await EvaluateAsync(ai, rule.Id, rule.Symbol, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { logger.AlertRuleFailed(rule.Id.Value, ex); }
        }
    }

    private async Task EvaluateEconomicAsync(AlertRuleId ruleId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var evaluator = scope.ServiceProvider.GetRequiredService<EconomicAlertEvaluator>();

        var rule = await db.AlertRules.FirstOrDefaultAsync(r => r.Id == ruleId, ct);
        if (rule is null) return;

        var raised = await evaluator.EvaluateAsync(rule, ct);
        if (raised is not null) logger.AlertRaised(rule.Id.Value, raised.Severity);
        await db.SaveChangesAsync(ct);
    }

    private async Task EvaluateAsync(IAiFeatureService ai, AlertRuleId ruleId, string symbol, CancellationToken ct)
    {
        var result = await ai.AssessSymbolAlertAsync(symbol, AlertConstants.AssessMaxTokens, ct);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var rule = await db.AlertRules.FirstOrDefaultAsync(r => r.Id == ruleId, ct);
        if (rule is null) return;

        var now = timeProvider.GetUtcNow();
        rule.MarkEvaluated(now);
        if (result.Success)
        {
            var assessment = AlertJson.Parse(result.Text);
            if (assessment is { Alert: true })
            {
                rule.Raise(new AlertSeverity(assessment.Severity), assessment.Message, now);
                logger.AlertRaised(rule.Id.Value, assessment.Severity);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
