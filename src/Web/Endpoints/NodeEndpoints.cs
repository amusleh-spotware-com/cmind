using System.Text;
using Core;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Web.Endpoints;

public record CreateNodeRequest(
    string Name, string Host, int SshPort, string SshUser,
    string SshPrivateKey, string? SshKeyPassphrase, NodeMode Mode,
    string DataDirPath, int MaxInstances);

public static class NodeEndpoints
{
    public static IEndpointRouteBuilder MapNodeEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/nodes").RequireAuthorization("AdminOrAbove");

        g.MapGet("/", async (CtwDbContext db) =>
            await db.Nodes.Include(n => n.LatestStats)
                .Select(n => new
                {
                    n.Id, n.Name, n.Host, n.Mode, n.Status, n.MaxInstances,
                    Stats = n.LatestStats
                }).ToListAsync());

        g.MapPost("/", async (CreateNodeRequest req, CtwDbContext db, ISecretProtector p) =>
        {
            var node = new Node
            {
                Name = req.Name,
                Host = req.Host,
                SshPort = req.SshPort,
                SshUser = req.SshUser,
                EncryptedSshKey = p.Protect(Encoding.UTF8.GetBytes(req.SshPrivateKey), "node.ssh.key"),
                EncryptedSshKeyPassphrase = string.IsNullOrEmpty(req.SshKeyPassphrase)
                    ? null
                    : p.Protect(Encoding.UTF8.GetBytes(req.SshKeyPassphrase), "node.ssh.pass"),
                Mode = req.Mode,
                DataDirPath = req.DataDirPath,
                MaxInstances = req.MaxInstances
            };
            db.Nodes.Add(node);
            await db.SaveChangesAsync();
            return Results.Ok(new { node.Id });
        });

        g.MapDelete("/{id:guid}", async (Guid id, CtwDbContext db, IContainerDispatcher dispatcher) =>
        {
            var node = await db.Nodes.Include(n => n.LatestStats).FirstOrDefaultAsync(n => n.Id == id);
            if (node is null) return Results.NotFound();
            node.Status = NodeStatus.Decommissioning;
            await db.SaveChangesAsync();

            var running = await db.Instances
                .Where(i => i.NodeId == id && (i.Status == InstanceStatus.Running || i.Status == InstanceStatus.Starting))
                .ToListAsync();
            foreach (var i in running)
            {
                try { await dispatcher.StopAsync(i, default); } catch { /* swallow */ }
                i.Status = InstanceStatus.Stopped;
                i.StoppedAt = DateTimeOffset.UtcNow;
            }
            await db.SaveChangesAsync();
            db.Nodes.Remove(node);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapPost("/{id:guid}/clean-backtest-data", async (Guid id, CtwDbContext db,
            IContainerDispatcher dispatcher) =>
        {
            var node = await db.Nodes.FindAsync(id);
            if (node is null) return Results.NotFound();
            await dispatcher.CleanBacktestDataAsync(node, null, default);
            return Results.Ok();
        });

        return app;
    }
}
