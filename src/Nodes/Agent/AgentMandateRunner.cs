using Core;
using Core.Agent;
using Core.Ai;
using Core.Constants;
using Core.Logging;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Nodes.Agent;

// Runs one mandate cycle. Shared by the scheduled PortfolioAgentService (per due mandate) and the on-demand
// "Run now" endpoint, so both paths behave identically and write to the same decision journal.
public sealed class AgentMandateRunner(
    DataContext db,
    IAiFeatureService ai,
    IAgentExecutor executor,
    ILogger<AgentMandateRunner> logger,
    TimeProvider timeProvider) : IAgentMandateRunner
{
    public async Task RunOnceAsync(AgentMandateId mandateId, UserId actor, CancellationToken ct)
    {
        if (!ai.Enabled) return;

        var mandate = await db.AgentMandates.Include(m => m.CBot)
            .FirstOrDefaultAsync(m => m.Id == mandateId && m.UserId == actor, ct);
        if (mandate is null) return;

        var currentParams = await db.ParamSets
            .Where(p => p.CBotId == mandate.CBotId && p.UserId == mandate.UserId)
            .OrderByDescending(p => p.CreatedAt).Select(p => p.JsonContent).FirstOrDefaultAsync(ct) ?? "{}";

        var lastReport = await db.Instances.OfType<CompletedBacktestInstance>()
            .Where(i => i.CBotId == mandate.CBotId && i.UserId == mandate.UserId)
            .OrderByDescending(i => i.CreatedAt).Select(i => i.ReportJson).FirstOrDefaultAsync(ct);

        var objective = BuildObjective(mandate);
        var result = await ai.ProposeAgentActionAsync(
            mandate.CBot.Name, objective, currentParams, lastReport, AgentConstants.ActionMaxTokens, ct);

        mandate.RecordRun(timeProvider.GetUtcNow());

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

        var proposal = mandate.AddProposal(AgentConstants.ProposalKindBacktest,
            action.Reasoning, action.ParametersJson, action.Name);
        await db.SaveChangesAsync(ct);
        logger.AgentProposalCreated(proposal.Id.Value, mandate.Id.Value, mandate.Autonomy);

        if (mandate.Autonomy == AgentAutonomy.Auto)
            await executor.ExecuteAsync(proposal.Id, mandate.UserId, ct);
    }

    private static string BuildObjective(AgentMandate mandate) =>
        $"{mandate.Objective}\nRisk per trade: {mandate.RiskPercentPerTrade}%. " +
        $"Max drawdown: {mandate.MaxDrawdownPercent}%. Symbol: {mandate.Symbol}, timeframe: {mandate.Timeframe}.";
}
