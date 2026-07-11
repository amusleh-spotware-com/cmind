using Core;
using Core.Constants;
using Core.Logging;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Nodes.CopyTrading;

// Phase 3 execution transparency: drains the copy host's execution facts from the in-memory channel and
// persists them to the CopyExecution append-only log. Runs only when App:Copy:TransparencyEnabled. The
// host never touches the DB — this out-of-band drainer keeps the trading hot path free of persistence I/O.
public sealed class CopyExecutionDrainer(
    ChannelCopyEventSink sink,
    IServiceScopeFactory scopeFactory,
    ILogger<CopyExecutionDrainer> log,
    TimeProvider timeProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CopyDefaults.CopyExecutionDrainInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await DrainOnceAsync(stoppingToken); }
            catch (Exception ex) { log.CopyExecutionDrainFailed(ex); }
        }

        try { await DrainOnceAsync(CancellationToken.None); } // final flush on shutdown
        catch (Exception ex) { log.CopyExecutionDrainFailed(ex); }
    }

    // Flushes everything currently buffered in the sink to the CopyExecution log in batches. Internal so the
    // drain-to-DB path is asserted directly against a real Postgres without driving the background timer.
    internal async Task DrainOnceAsync(CancellationToken ct)
    {
        while (true)
        {
            var batch = new List<CopyExecution>(CopyDefaults.CopyExecutionDrainBatchSize);
            while (batch.Count < CopyDefaults.CopyExecutionDrainBatchSize && sink.TryRead(out var record))
                batch.Add(CopyExecution.From(record));
            if (batch.Count == 0) return;

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            db.CopyExecutions.AddRange(batch);
            await db.SaveChangesAsync(ct);

            if (batch.Count < CopyDefaults.CopyExecutionDrainBatchSize) return; // channel drained
        }
    }
}
