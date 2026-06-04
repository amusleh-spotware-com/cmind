using Core;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Web.Hubs;

[Authorize]
public sealed class LogsHub(DataContext db, IContainerDispatcherFactory factory) : Hub
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
}
