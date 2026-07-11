using Core;
using Core.Constants;
using Core.CopyTrading;
using Core.Logging;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Nodes.CopyTrading;

// 2b notification routing: drains the copy host's operational notifications from the channel, resolves the
// owning user from each profile, and persists them to the CopyNotification feed. Out-of-band so the host
// never touches the DB. Only a record whose profile still exists (owner resolvable) is persisted.
public sealed class CopyNotificationDrainer(
    ChannelCopyNotificationSink sink,
    IServiceScopeFactory scopeFactory,
    ILogger<CopyNotificationDrainer> log,
    TimeProvider timeProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CopyDefaults.CopyExecutionDrainInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await DrainOnceAsync(stoppingToken); }
            catch (Exception ex) { log.CopyNotificationDrainFailed(ex); }
        }

        try { await DrainOnceAsync(CancellationToken.None); } // final flush on shutdown
        catch (Exception ex) { log.CopyNotificationDrainFailed(ex); }
    }

    internal async Task DrainOnceAsync(CancellationToken ct)
    {
        while (true)
        {
            var records = new List<CopyNotificationRecord>(CopyDefaults.CopyExecutionDrainBatchSize);
            while (records.Count < CopyDefaults.CopyExecutionDrainBatchSize && sink.TryRead(out var record))
                records.Add(record);
            if (records.Count == 0) return;

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();

            var profileIds = records.Select(r => r.ProfileId).Distinct().ToList();
            var owners = await db.CopyProfiles.Where(p => profileIds.Contains(p.Id))
                .Select(p => new { p.Id, p.UserId }).ToDictionaryAsync(x => x.Id, x => x.UserId, ct);

            foreach (var record in records)
                if (owners.TryGetValue(record.ProfileId, out var userId))
                    db.CopyNotifications.Add(CopyNotification.From(record, userId));

            await db.SaveChangesAsync(ct);
            if (records.Count < CopyDefaults.CopyExecutionDrainBatchSize) return; // channel drained
        }
    }
}
