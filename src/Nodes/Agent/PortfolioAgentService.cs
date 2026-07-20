using Core;
using Core.Agent;
using Core.Ai;
using Core.Logging;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nodes.Agent;

public sealed class PortfolioAgentService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<AppOptions> options,
    ILogger<PortfolioAgentService> logger,
    TimeProvider timeProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(options.CurrentValue.Agent.Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }

            try { await ScanAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.AgentCycleFailed(ex); }
        }
    }

    private async Task ScanAsync(CancellationToken ct)
    {
        var config = options.CurrentValue.Agent;
        if (!config.Enabled) return;

        using var scope = scopeFactory.CreateScope();
        var ai = scope.ServiceProvider.GetRequiredService<IAiFeatureService>();
        if (!ai.Enabled) return;

        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var runner = scope.ServiceProvider.GetRequiredService<IAgentMandateRunner>();

        var cutoff = timeProvider.GetUtcNow() - config.Interval;
        var due = await db.AgentMandates
            .Where(m => m.Enabled && (m.LastRunAt == null || m.LastRunAt < cutoff))
            .OrderBy(m => m.LastRunAt)
            .Take(config.MaxProposalsPerCycle)
            .Select(m => new { m.Id, m.UserId })
            .ToListAsync(ct);

        foreach (var mandate in due)
        {
            ct.ThrowIfCancellationRequested();
            try { await runner.RunOnceAsync(mandate.Id, mandate.UserId, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { logger.AgentMandateFailed(mandate.Id.Value, ex); }
        }
    }
}
