using Core;
using Core.CopyTrading;
using Core.Logging;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Nodes.CopyTrading;

// Phase 3 execution transparency: drains the copy host's execution facts from the channel to the
// CopyExecution append-only log. Runs only when App:Copy:TransparencyEnabled. The host never touches the DB.
public sealed class CopyExecutionDrainer(
    ChannelCopyEventSink sink,
    IServiceScopeFactory scopeFactory,
    ILogger<CopyExecutionDrainer> log,
    TimeProvider timeProvider)
    : CopyChannelDrainer<CopyExecutionRecord>(sink, scopeFactory, timeProvider)
{
    protected override Task PersistAsync(DataContext db, IReadOnlyList<CopyExecutionRecord> batch, CancellationToken ct)
    {
        db.CopyExecutions.AddRange(batch.Select(CopyExecution.From));
        return Task.CompletedTask;
    }

    protected override void OnDrainFailed(Exception ex) => log.CopyExecutionDrainFailed(ex);
}
