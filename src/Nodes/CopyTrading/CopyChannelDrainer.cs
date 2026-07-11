using Core.Constants;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Nodes.CopyTrading;

// Shared drain pump behind the copy host's out-of-band sinks: on an interval, drains everything buffered in
// the channel and persists it to the DB in batches, off the trading hot path. Subclasses supply only the
// persistence and the failure log. A final flush runs on shutdown.
public abstract class CopyChannelDrainer<T>(
    CopyRecordChannel<T> channel,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CopyDefaults.CopyExecutionDrainInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await DrainOnceAsync(stoppingToken); }
            catch (Exception ex) { OnDrainFailed(ex); }
        }

        try { await DrainOnceAsync(CancellationToken.None); } // final flush on shutdown
        catch (Exception ex) { OnDrainFailed(ex); }
    }

    internal async Task DrainOnceAsync(CancellationToken ct)
    {
        while (true)
        {
            var batch = new List<T>(CopyDefaults.CopyExecutionDrainBatchSize);
            while (batch.Count < CopyDefaults.CopyExecutionDrainBatchSize && channel.TryRead(out var record))
                batch.Add(record);
            if (batch.Count == 0) return;

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            await PersistAsync(db, batch, ct);
            await db.SaveChangesAsync(ct);

            if (batch.Count < CopyDefaults.CopyExecutionDrainBatchSize) return; // channel drained
        }
    }

    // Add the batch to the DbContext (the base saves). Async so a subclass can resolve related data first.
    protected abstract Task PersistAsync(DataContext db, IReadOnlyList<T> batch, CancellationToken ct);

    protected abstract void OnDrainFailed(Exception ex);
}
