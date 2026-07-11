using Core;
using Core.CopyTrading;
using Core.Logging;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Nodes.CopyTrading;

// 2b notification routing: drains the copy host's operational notifications from the channel, resolves the
// owning user from each profile, and persists them to the per-owner CopyNotification feed. A record whose
// profile no longer exists (owner unresolvable) is dropped rather than orphaned.
public sealed class CopyNotificationDrainer(
    ChannelCopyNotificationSink sink,
    IServiceScopeFactory scopeFactory,
    ILogger<CopyNotificationDrainer> log,
    TimeProvider timeProvider)
    : CopyChannelDrainer<CopyNotificationRecord>(sink, scopeFactory, timeProvider)
{
    protected override async Task PersistAsync(DataContext db, IReadOnlyList<CopyNotificationRecord> batch, CancellationToken ct)
    {
        var profileIds = batch.Select(r => r.ProfileId).Distinct().ToList();
        var owners = await db.CopyProfiles.Where(p => profileIds.Contains(p.Id))
            .Select(p => new { p.Id, p.UserId }).ToDictionaryAsync(x => x.Id, x => x.UserId, ct);

        foreach (var record in batch)
            if (owners.TryGetValue(record.ProfileId, out var userId))
                db.CopyNotifications.Add(CopyNotification.From(record, userId));
    }

    protected override void OnDrainFailed(Exception ex) => log.CopyNotificationDrainFailed(ex);
}
