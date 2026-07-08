using Core;
using Core.Agent;
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

namespace Nodes.Agent;

public sealed class PortfolioAgentService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<AppOptions> options,
    ILogger<PortfolioAgentService> logger) : BackgroundService
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
        var executor = scope.ServiceProvider.GetRequiredService<IAgentExecutor>();

        var cutoff = DateTimeOffset.UtcNow - config.Interval;
        var due = await db.AgentMandates.Include(m => m.CBot)
            .Where(m => m.Enabled && (m.LastRunAt == null || m.LastRunAt < cutoff))
            .OrderBy(m => m.LastRunAt)
            .Take(config.MaxProposalsPerCycle)
            .ToListAsync(ct);

        foreach (var mandate in due)
        {
            ct.ThrowIfCancellationRequested();
            try { await RunMandateAsync(db, ai, executor, mandate, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { logger.AgentMandateFailed(mandate.Id.Value, ex); }
        }
    }

    private async Task RunMandateAsync(
        DataContext db, IAiFeatureService ai, IAgentExecutor executor, AgentMandate mandate, CancellationToken ct)
    {
        var currentParams = await db.ParamSets
            .Where(p => p.CBotId == mandate.CBotId && p.UserId == mandate.UserId)
            .OrderByDescending(p => p.CreatedAt).Select(p => p.JsonContent).FirstOrDefaultAsync(ct) ?? "{}";

        var lastReport = await db.Instances.OfType<CompletedBacktestInstance>()
            .Where(i => i.CBotId == mandate.CBotId && i.UserId == mandate.UserId)
            .OrderByDescending(i => i.CreatedAt).Select(i => i.ReportJson).FirstOrDefaultAsync(ct);

        var objective = BuildObjective(mandate);
        var result = await ai.ProposeAgentActionAsync(
            mandate.CBot.Name, objective, currentParams, lastReport, AgentConstants.ActionMaxTokens, ct);

        mandate.LastRunAt = DateTimeOffset.UtcNow;

        if (!result.Success)
        {
            await db.SaveChangesAsync(ct);
            return;
        }

        var action = AgentJson.ParseAction(result.Text);
        if (action is null)
        {
            await db.SaveChangesAsync(ct);
            return;
        }

        var proposal = new AgentProposal
        {
            MandateId = mandate.Id,
            UserId = mandate.UserId,
            Kind = AgentConstants.ProposalKindBacktest,
            Reasoning = action.Reasoning,
            PayloadJson = action.ParametersJson,
            ProposedName = action.Name,
            Status = AgentProposalStatus.Pending
        };
        db.AgentProposals.Add(proposal);
        await db.SaveChangesAsync(ct);
        logger.AgentProposalCreated(proposal.Id.Value, mandate.Id.Value, mandate.Autonomy.ToString());

        if (mandate.Autonomy == AgentAutonomy.Auto)
            await executor.ExecuteAsync(proposal.Id, mandate.UserId, ct);
    }

    private static string BuildObjective(AgentMandate mandate) =>
        $"{mandate.Objective}\nRisk per trade: {mandate.RiskPercentPerTrade}%. " +
        $"Max drawdown: {mandate.MaxDrawdownPercent}%. Symbol: {mandate.Symbol}, timeframe: {mandate.Timeframe}.";
}
