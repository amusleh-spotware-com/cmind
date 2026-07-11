using Core;
using Core.CopyTrading;
using Core.Domain;
using Core.Logging;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nodes.CopyTrading;

// Phase 4 money-manager performance fees: periodically settles each fee-configured destination's
// high-water-mark performance fee against its live equity and records a CopyFeeAccrual. Gated on
// App:Copy:FeesEnabled. The fee arithmetic lives on the CopyDestination aggregate (SettleFee); this service
// only supplies the polled equity, records the returned amount, and persists the advanced high-water-mark.
public sealed class CopyFeeSettlementService(
    IServiceScopeFactory scopeFactory,
    ICopyEquityReader equityReader,
    IOptionsMonitor<AppOptions> options,
    ILogger<CopyFeeSettlementService> log,
    TimeProvider timeProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.CurrentValue.Copy.FeeSettlementInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await SettleAsync(stoppingToken); }
            catch (Exception ex) { log.CopyFeeSettlementFailed(ex); }
        }
    }

    internal async Task SettleAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        // Settle only the profiles THIS node hosts (the supervisor's lease assigns each running profile to
        // exactly one node), so two nodes can never both settle the same destination and double-charge a fee.
        var node = ResolveNode();
        var profiles = await db.CopyProfiles.Include(p => p.Destinations)
            .Where(p => p.Status == CopyProfileStatus.Running && p.AssignedNode == node.Value
                && p.Destinations.Any(d => d.PerformanceFeePercent > 0))
            .ToListAsync(ct);

        foreach (var profile in profiles)
        {
            var changed = false;
            foreach (var destination in profile.Destinations.Where(d => d.PerformanceFeePercent > 0))
            {
                var ctid = await db.TradingAccounts.Where(a => a.Id == destination.DestinationAccountId)
                    .Select(a => a.CtidTraderAccountId).FirstOrDefaultAsync(ct);
                if (ctid is null) continue;

                var equity = await equityReader.ReadEquityAsync(ctid.Value, ct);
                if (equity is null) continue;

                var highWaterMarkBefore = destination.HighWaterMarkEquity;
                var fee = destination.SettleFee(equity.Value);
                if (destination.HighWaterMarkEquity != highWaterMarkBefore) changed = true;

                if (fee > 0)
                {
                    db.CopyFeeAccruals.Add(CopyFeeAccrual.Create(profile.Id.Value, destination.Id.Value,
                        profile.UserId, highWaterMarkBefore, equity.Value, destination.PerformanceFeePercent, fee,
                        timeProvider.GetUtcNow()));
                    log.CopyFeeAccrued(profile.Id.Value, destination.Id.Value, fee);
                    changed = true; // a charged fee always advances the mark, but be explicit so the save can't be skipped
                }
            }

            if (changed) await db.SaveChangesAsync(ct);
        }
    }

    private NodeIdentity ResolveNode()
    {
        var configured = options.CurrentValue.Copy.NodeName;
        return new NodeIdentity(string.IsNullOrWhiteSpace(configured) ? Environment.MachineName : configured);
    }
}
