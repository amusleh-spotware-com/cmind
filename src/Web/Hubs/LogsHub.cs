using System.Security.Claims;
using Core;
using Core.CopyTrading;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Web.Hubs;

[Authorize]
public sealed class LogsHub(DataContext db, IContainerDispatcherFactory factory, ICopyLogFeed copyLogFeed) : Hub
{
    public async IAsyncEnumerable<string> Tail(Guid instanceId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var iid = InstanceId.From(instanceId);
        var i = await db.Instances.Include(x => x.Node).FirstOrDefaultAsync(x => x.Id == iid, ct);
        if (i?.Node is null) yield break;
        await foreach (var line in factory.For(i).TailLogsAsync(i, ct))
            yield return line;
    }

    // Live activity tail for a running copy profile — the copy-trading equivalent of the container log tail
    // above, streamed from the in-process broker the copy host writes to. Scoped to the profile's owner so
    // one user can never read another's copy activity (the profile id alone is not an authorisation).
    public async IAsyncEnumerable<string> TailCopyProfile(Guid profileId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(claim, out var user)) yield break;

        var pid = CopyProfileId.From(profileId);
        var owns = await db.CopyProfiles.AnyAsync(p => p.Id == pid && p.UserId == UserId.From(user), ct);
        if (!owns) yield break;

        await foreach (var line in copyLogFeed.TailAsync(pid, ct))
            yield return line;
    }
}
