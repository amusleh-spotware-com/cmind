using Core;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Web.Hubs;

[Authorize]
public sealed class LogsHub(DataContext db, IContainerDispatcher dispatcher) : Hub
{
    public async IAsyncEnumerable<string> Tail(Guid instanceId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var iid = InstanceId.From(instanceId);
        var i = await db.Instances.Include(x => x.Node).FirstOrDefaultAsync(x => x.Id == iid, ct);
        if (i is null) yield break;
        await foreach (var line in dispatcher.TailLogsAsync(i, ct))
            yield return line;
    }
}
