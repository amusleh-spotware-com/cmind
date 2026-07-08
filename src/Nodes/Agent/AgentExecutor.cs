using Core;
using Core.Agent;
using Core.Constants;
using Core.Logging;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Nodes.Agent;

public sealed class AgentExecutor(
    DataContext db,
    ISecretProtector protector,
    INodeScheduler scheduler,
    IContainerDispatcherFactory factory,
    ILogger<AgentExecutor> logger) : IAgentExecutor
{
    public async Task<bool> ExecuteAsync(AgentProposalId proposalId, UserId actor, CancellationToken ct)
    {
        var proposal = await db.AgentProposals
            .Include(p => p.Mandate).ThenInclude(m => m.CBot)
            .FirstOrDefaultAsync(p => p.Id == proposalId, ct);
        if (proposal is null) return false;
        if (proposal.UserId != actor) return false;
        if (proposal.Status is AgentProposalStatus.Executed) return true;
        if (proposal.Status is AgentProposalStatus.Rejected) return false;

        try
        {
            var instanceId = await LaunchBacktestAsync(proposal, ct);
            proposal.Status = AgentProposalStatus.Executed;
            proposal.CreatedInstanceId = instanceId;
            proposal.DecidedAt = DateTimeOffset.UtcNow;
            proposal.DecidedByUserId = actor;
            proposal.FailureReason = null;
            await db.SaveChangesAsync(ct);
            logger.AgentProposalExecuted(proposalId.Value, instanceId.Value);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await MarkFailedAsync(proposal, actor, ex.Message, ct);
            logger.AgentProposalExecutionFailed(proposalId.Value, ex.Message);
            return false;
        }
    }

    private async Task<InstanceId> LaunchBacktestAsync(AgentProposal proposal, CancellationToken ct)
    {
        var mandate = proposal.Mandate;
        if (mandate.TradingAccountId is not { } accountId)
            throw new InvalidOperationException("mandate has no trading account");

        var account = await db.TradingAccounts.Include(t => t.CTid)
            .FirstOrDefaultAsync(t => t.Id == accountId && t.CTid.UserId == mandate.UserId, ct)
            ?? throw new InvalidOperationException("trading account not found");

        var paramSet = new ParamSet
        {
            UserId = mandate.UserId,
            CBotId = mandate.CBotId,
            Name = UniqueParamName(proposal.ProposedName, proposal.Id),
            JsonContent = proposal.PayloadJson
        };
        db.ParamSets.Add(paramSet);
        await db.SaveChangesAsync(ct);
        proposal.CreatedParamSetId = paramSet.Id;

        var node = await scheduler.PickNodeAsync(CliCommands.Backtest, ct)
            ?? throw new InvalidOperationException("no backtest node available");

        var starting = new StartingBacktestInstance
        {
            UserId = mandate.UserId,
            CBotId = mandate.CBotId,
            TradingAccountId = accountId,
            NodeId = node.Id,
            DockerImageTag = mandate.DockerImageTag,
            Symbol = mandate.Symbol,
            Timeframe = mandate.Timeframe,
            ParamSetId = paramSet.Id,
            BacktestSettingsJson = mandate.BacktestSettingsJson
        };
        db.Instances.Add(starting);
        await db.SaveChangesAsync(ct);
        starting.Node = node;

        var algo = protector.Unprotect(mandate.CBot.EncryptedAlgo, EncryptionPurposes.CbotAlgo);
        try
        {
            var containerId = await factory.For(node).StartAsync(starting, algo, paramSet.JsonContent, ct);
            db.Instances.Remove(starting);
            var running = new RunningBacktestInstance
            {
                UserId = mandate.UserId,
                CBotId = mandate.CBotId,
                TradingAccountId = accountId,
                NodeId = node.Id,
                DockerImageTag = mandate.DockerImageTag,
                Symbol = mandate.Symbol,
                Timeframe = mandate.Timeframe,
                ParamSetId = paramSet.Id,
                BacktestSettingsJson = mandate.BacktestSettingsJson,
                ContainerId = containerId,
                StartedAt = DateTimeOffset.UtcNow,
                DataDirSubPath = starting.DataDirSubPath
            };
            db.Instances.Add(running);
            await db.SaveChangesAsync(ct);
            return running.Id;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            db.Instances.Remove(starting);
            await db.SaveChangesAsync(ct);
            throw;
        }
    }

    private async Task MarkFailedAsync(AgentProposal proposal, UserId actor, string reason, CancellationToken ct)
    {
        proposal.Status = AgentProposalStatus.Failed;
        proposal.FailureReason = Clip(reason);
        proposal.DecidedAt = DateTimeOffset.UtcNow;
        proposal.DecidedByUserId = actor;
        try { await db.SaveChangesAsync(ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { logger.AgentMandateFailed(proposal.MandateId.Value, ex); }
    }

    private static string UniqueParamName(string proposed, AgentProposalId proposalId)
    {
        var suffix = proposalId.Value.ToString("N")[..6];
        var name = string.IsNullOrWhiteSpace(proposed) ? "AI proposal" : proposed.Trim();
        var composed = $"{name} [{suffix}]";
        return composed.Length <= 256 ? composed : composed[..256];
    }

    private static string Clip(string value) => value.Length <= 512 ? value : value[..512];
}
