using Core;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Web.Hubs;

[Authorize]
public sealed class LogsHub : Hub
{
    private readonly CtwDbContext _db;
    private readonly IContainerDispatcher _dispatcher;
    public LogsHub(CtwDbContext db, IContainerDispatcher dispatcher)
    {
        _db = db;
        _dispatcher = dispatcher;
    }

    public async IAsyncEnumerable<string> Tail(Guid instanceId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var iid = InstanceId.From(instanceId);
        var i = await _db.Instances.Include(x => x.Node).FirstOrDefaultAsync(x => x.Id == iid, ct);
        if (i is null) yield break;
        await foreach (var line in _dispatcher.TailLogsAsync(i, ct))
            yield return line;
    }
}
