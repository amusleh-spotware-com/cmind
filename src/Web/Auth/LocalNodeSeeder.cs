using Core;
using Core.Logging;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Web.Auth;

public sealed class LocalNodeSeeder(
    IServiceScopeFactory sf,
    IOptionsMonitor<AppOptions> options,
    ILogger<LocalNodeSeeder> log) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var opts = options.CurrentValue.LocalNode;
        if (!opts.Enabled) return;

        using var scope = sf.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        if (await db.Nodes.OfType<LocalNode>().IgnoreQueryFilters().AnyAsync(ct)) return;

        var node = LocalNode.Create(opts.Name, opts.WorkRoot, opts.MaxInstances, enabled: true);
        db.Nodes.Add(node);
        await db.SaveChangesAsync(ct);
        log.LocalNodeSeeded(opts.Name);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
